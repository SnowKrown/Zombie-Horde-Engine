# UZE: Ultimate Zombie & Horde Engine - Guia Tecnica de Arquitectura (v1.0)

## 1. Vision General y Especificaciones del Entorno
**UZE (Ultimate Zombie & Horde Engine)** es un framework modular de alto rendimiento para la Unity Asset Store disenado para simular, navegar y renderizar desde zombis individuales reactivos hasta hordas masivas de miles de agentes sin requerir componentes pesados por instancia (`Animator`, `SkinnedMeshRenderer`, `NavMeshAgent` o `Rigidbody` permanentes).

* **Version de Desarrollo Base:** Unity 2022.3 LTS (para garantizar compatibilidad con la Asset Store).
* **Version de Validacion Superior:** Unity 6 (`UNITY_6000_0_OR_NEWER`).
* **Render Pipeline:** Universal Render Pipeline (URP 14+).
* **Stack Tecnologico:** Unity C# Job System, Burst Compiler, `Unity.Collections`, `Unity.Mathematics`, GPU Instancing (`MaterialPropertyBlock` / `DrawMeshInstanced` / `RenderMeshInstanced`) y Shaders HLSL personalizados.
* **Estructura de Ensamblados:**
  * `UZE.Runtime` (`Runtime/`): Toda la logica desacoplada, Jobs, estructuras blittable y despachadores.
  * `UZE.Editor` (`Editor/`): Asistentes de horneado (`ZombieBakeWizardWindow`), inspectores y herramientas de autor.
  * `UZE.Samples` (`Samples~/`): Controladores de demostracion, assets de ejemplo y escenas de prueba.

---

## 2. Arquitectura de Desacoplamiento con el Jugador
El plugin **NO** incluye un sistema de locomocion propio para el jugador para evitar acoplamientos y conflictos con controladores de terceros. Toda interaccion entre los zombis y el jugador se realiza mediante contratos de interfaz definidos en `UZE.Core.Contracts`:

```csharp
namespace UZE.Core.Contracts
{
    public enum StatusEffectType : byte
    {
        None = 0,
        Slowed = 1,
        Immobilized = 2,
        Bleeding = 3,
        Infected = 4
    }

    public interface IStatusEffectReceiver
    {
        void ApplyStatusEffect(StatusEffectType type, float duration, float magnitude);
    }

    public interface IGrabbableTarget
    {
        bool CanBeGrabbed { get; }
        void OnGrabbed(UnityEngine.Transform attackerTransform, UnityEngine.Vector3 attachOffset);
        void OnReleased();
    }

    public interface IDamageableTarget
    {
        void TakeDamage(float amount, UnityEngine.Vector3 hitPoint, UnityEngine.Vector3 hitDirection);
    }
}
```

---

## 3. Navegacion 3D Multicapa: NavMesh-Polygon Flow Field + Short-Horizon Funnel
Para soportar mapas complejos, multiples pisos superpuestos, rampas y escaleras sin la sobrecarga de memoria de una cuadricula 2D gigante, el Flow Field opera directamente sobre el grafo topologico 3D de los poligonos del NavMesh horneado.

### 3.1 Estructuras Blittable del Grafo de Poligonos
```csharp
using Unity.Mathematics;

namespace UZE.Navigation
{
    public struct NavPolyNode
    {
        public float3 centroid;
        public int edgeStartIndex;
        public int edgeCount;
    }

    public struct NavPolyEdge
    {
        public int toPolyIndex;
        public float3 leftVertex;
        public float3 rightVertex;
        public float3 portalCenter;
        public float edgeCost;
    }
}
```

### 3.2 Propagacion Dijkstra sobre el Grafo NavMesh (Burst Job)
```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace UZE.Navigation
{
    [BurstCompile]
    public struct GeneratePolyFlowFieldWithEdgesJob : IJob
    {
        public int targetPolyIndex;
        [ReadOnly] public NativeArray<NavPolyNode> nodes;
        [ReadOnly] public NativeArray<NavPolyEdge> edges;

        public NativeArray<float> accumulatedCosts;
        [WriteOnly] public NativeArray<int> bestNextEdge;

        public void Execute()
        {
            InitializeCosts();
            PropagateDijkstra();
        }

        private void InitializeCosts()
        {
            int totalNodes = nodes.Length;
            for (int i = 0; i < totalNodes; i++)
            {
                accumulatedCosts[i] = float.MaxValue;
                bestNextEdge[i] = -1;
            }

            accumulatedCosts[targetPolyIndex] = 0.0f;
        }

        private void PropagateDijkstra()
        {
            NativeQueue<int> frontier = new NativeQueue<int>(Allocator.Temp);
            frontier.Enqueue(targetPolyIndex);

            while (frontier.TryDequeue(out int currentPoly))
                RelaxNeighbors(currentPoly, ref frontier);

            frontier.Dispose();
        }

        private void RelaxNeighbors(int currentPoly, ref NativeQueue<int> frontier)
        {
            float currentCost = accumulatedCosts[currentPoly];
            NavPolyNode node = nodes[currentPoly];

            for (int i = 0; i < node.edgeCount; i++)
            {
                int edgeIndex = node.edgeStartIndex + i;
                NavPolyEdge edge = edges[edgeIndex];
                int neighbor = edge.toPolyIndex;
                float newCost = currentCost + edge.edgeCost;

                if (newCost < accumulatedCosts[neighbor])
                {
                    accumulatedCosts[neighbor] = newCost;
                    bestNextEdge[neighbor] = FindOppositeEdgeIndex(neighbor, currentPoly);
                    frontier.Enqueue(neighbor);
                }
            }
        }

        private int FindOppositeEdgeIndex(int fromPoly, int toPoly)
        {
            NavPolyNode node = nodes[fromPoly];
            for (int j = 0; j < node.edgeCount; j++)
            {
                int index = node.edgeStartIndex + j;
                if (edges[index].toPolyIndex == toPoly)
                    return index;
            }

            return -1;
        }
    }
}
```

### 3.3 Algoritmo Short-Horizon Funnel (String Pulling en Burst)
Evita trayectorias en zigzag entre los centros de las aristas evaluando una ventana de hasta `maxLookahead = 3` portales y aplicando compensacion por el radio del agente (`agentRadius`):

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace UZE.Navigation
{
    [BurstCompile]
    public static class FunnelSteeringCalculator
    {
        private static float TriArea2D(float3 a, float3 b, float3 c)
        {
            return (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z);
        }

        public static float3 CalculateSteeringPoint(
            float3 agentPos,
            int currentPolyIndex,
            int targetPolyIndex,
            float3 playerPos,
            float agentRadius,
            int maxLookahead,
            [ReadOnly] NativeArray<NavPolyNode> nodes,
            [ReadOnly] NativeArray<NavPolyEdge> edges,
            [ReadOnly] NativeArray<int> bestNextEdge)
        {
            if (currentPolyIndex == targetPolyIndex)
                return playerPos;

            float3 apex = agentPos;
            float3 portalLeft = agentPos;
            float3 portalRight = agentPos;
            int activePoly = currentPolyIndex;
            bool isInitialized = false;

            for (int step = 0; step < maxLookahead; step++)
            {
                if (activePoly == targetPolyIndex)
                    return ResolveTarget(apex, portalLeft, portalRight, playerPos);

                if (StepPortal(ref activePoly, ref apex, ref portalLeft, ref portalRight, ref isInitialized, agentRadius, edges, bestNextEdge, out float3 exitPoint))
                    return exitPoint;
            }

            return (portalLeft + portalRight) * 0.5f;
        }

        private static bool StepPortal(
            ref int activePoly,
            ref float3 apex,
            ref float3 portalLeft,
            ref float3 portalRight,
            ref bool isInitialized,
            float agentRadius,
            [ReadOnly] NativeArray<NavPolyEdge> edges,
            [ReadOnly] NativeArray<int> bestNextEdge,
            out float3 exitPoint)
        {
            exitPoint = float3.zero;
            int edgeIndex = bestNextEdge[activePoly];
            if (edgeIndex < 0)
                return false;

            NavPolyEdge edge = edges[edgeIndex];
            ComputeShrunkPortal(edge, agentRadius, out float3 nextLeft, out float3 nextRight);
            activePoly = edge.toPolyIndex;

            if (!isInitialized)
            {
                portalLeft = nextLeft;
                portalRight = nextRight;
                isInitialized = true;
                return false;
            }

            return UpdatePortalBounds(ref apex, ref portalLeft, ref portalRight, nextLeft, nextRight, out exitPoint);
        }

        private static void ComputeShrunkPortal(NavPolyEdge edge, float agentRadius, out float3 shrunkLeft, out float3 shrunkRight)
        {
            float3 edgeDelta = edge.rightVertex - edge.leftVertex;
            float portalWidth = math.length(edgeDelta);
            float3 dir = edgeDelta / math.max(portalWidth, 0.0001f);
            float shrink = math.min(agentRadius, portalWidth * 0.35f);

            shrunkLeft = edge.leftVertex + dir * shrink;
            shrunkRight = edge.rightVertex - dir * shrink;
        }

        private static bool UpdatePortalBounds(
            ref float3 apex,
            ref float3 portalLeft,
            ref float3 portalRight,
            float3 nextLeft,
            float3 nextRight,
            out float3 exitPoint)
        {
            exitPoint = float3.zero;

            if (TriArea2D(apex, portalRight, nextRight) <= 0.0f)
            {
                if (math.all(apex == portalRight) || TriArea2D(apex, portalLeft, nextRight) > 0.0f)
                {
                    portalRight = nextRight;
                }
                else
                {
                    exitPoint = portalLeft;
                    return true;
                }
            }

            if (TriArea2D(apex, portalLeft, nextLeft) >= 0.0f)
            {
                if (math.all(apex == portalLeft) || TriArea2D(apex, portalRight, nextLeft) < 0.0f)
                {
                    portalLeft = nextLeft;
                }
                else
                {
                    exitPoint = portalRight;
                    return true;
                }
            }

            return false;
        }

        private static float3 ResolveTarget(float3 apex, float3 left, float3 right, float3 target)
        {
            if (TriArea2D(apex, left, target) <= 0.0f && TriArea2D(apex, right, target) >= 0.0f)
                return target;

            if (TriArea2D(apex, left, target) > 0.0f)
                return left;

            return right;
        }
    }
}
```

---

## 4. Dinamica de Multitudes, Separacion Espacial y Presion de Estampida
Para evitar comparaciones $O(N^2)$, se utiliza un **Spatial Hash Grid** basado en `NativeParallelMultiHashMap<int, int>`.

### 4.1 Hash Espacial y Separacion Lateral
La fuerza de separacion se filtra proyectandola perpendicularmente a la direccion de avance para evitar que los agentes frontales sean empujados hacia atras y colapsen los pasos estrechos:

```csharp
using Unity.Mathematics;

namespace UZE.Crowd
{
    public static class SpatialHashGrid
    {
        public static int Hash(float2 position, float cellSize)
        {
            int2 gridPos = (int2)math.floor(position / cellSize);
            return (gridPos.x * 73856093) ^ (gridPos.y * 19349663);
        }

        public static float3 FilterSeparation(float3 rawSeparation, float3 forwardDir, float backwardDamping = 0.1f)
        {
            float dotForward = math.dot(rawSeparation, forwardDir);
            if (dotForward < 0.0f)
                rawSeparation -= forwardDir * (dotForward * (1.0f - backwardDamping));

            return rawSeparation;
        }
    }
}
```

### 4.2 Gradiente de Presion, Modulacion de Velocidad y Tropiezos (Crowd Stress)
Durante la evaluacion de celdas vecinas en `EvaluateHordeCrowdStressJob`, se acumulan tres magnitudes:
1. **`rearPressure`**: Vecinos posteriores empujando en la direccion de avance ($\text{dot} > 0.05$). Acelera al agente hasta `maxSurgeMultiplier` ($1.45\times$).
2. **`frontResistance`**: Vecinos anteriores obstruyendo el avance ($\text{dot} < -0.05$). Reduce la velocidad hasta `minCongestionMultiplier` ($0.25\times$).
3. **`lateralForce`**: Fuerza de cizalladura ortogonal.

Si las tensiones sobrepasan los umbrales configurados modulados por el valor `poise` del arquetipo:
* `rearPressure > stumbleRearThreshold`: Provoca estado de **Tropiezo Frontal** (inercia rapida incontrolada hacia adelante).
* `rearPressure + frontResistance > crowdCrushThreshold`: Provoca estado de **Forcejeo / Compresion**.
* `math.length(lateralForce) > lateralInstabilityLimit`: Provoca estado de **Desequilibrio Lateral**.

---

## 5. Estados de IA, Percepcion Sensorial y Ataques Especiales

### 5.1 Maquina de Estados Ligera
```csharp
namespace UZE.Core
{
    public enum ZombieState : byte
    {
        Idle = 0,
        Wandering = 1,
        Investigating = 2,
        AggroHorde = 3,
        LungeWindup = 4,
        LungeAttack = 5
    }
}
```

### 5.2 Registro de Eventos Acusticos (`NoiseEventRegistry`)
* Mantiene una `NativeList<NoiseEvent>` por cuadro (`float3 position`, `float radiusSq`, `float alertThreshold`).
* `EvaluateHearingJob` (`IJobParallelFor`) evalua `math.distancesq` contra todos los agentes que no esten en alerta.
* Si `alertThreshold >= 0.8f`, el agente transiciona directamente a `AggroHorde`; en caso contrario, transiciona a `Investigating`.

### 5.3 Vision Conica y `RaycastCommand` por Time-Slicing (`VisionRayScheduler`)
* Divide el procesamiento de la horda en 4 lotes temporales rotativos (25% de agentes evaluados por cuadro).
* **Paso 1 (`FilterVisionJob`):** Filtra por distancia al cuadrado (`thresholdSq`) y producto punto (`dotProduct >= cosHalfFov`). Configura buffers de `RaycastCommand` con `QueryParameters` unicamente para agentes con orientacion valida.
* **Paso 2 (`RaycastCommand.ScheduleBatch`):** Despacho asincrono de rayos fisicos en hilos de trabajo contra `obstacleLayerMask`.
* **Paso 3 (`ProcessVisionHitsJob`):** Si `hit.colliderInstanceID == 0`, la linea visual esta libre de obstaculos y el agente transiciona a `AggroHorde`.

### 5.4 Ataques Especiales con Orientacion Previa y Root Motion Horneado
* **Orientacion de Ataque (`OrientLungeWindupJob`):** Al activarse el ataque, el agente entra en `ZombieState.LungeWindup`, rotando horizontalmente en el plano XZ hacia el objetivo con una velocidad angular configurada (`turnSpeedRadiansPerSec`). Solo cuando el vector `forward` y la direccion al objetivo quedan alineados dentro del umbral (`cosAlignThreshold`), transiciona a `ZombieState.LungeAttack` e inicia el temporizador en 0.
* **Lanzamiento e Impulso (`ApplyRootMotionJob`):** Al hornear animaciones de embestida o salto, se extraen estructuras `RootMotionData` (`float3 translationDelta`, `float rotationDeltaY`) por cuadro. Durante `ZombieState.LungeAttack`, el Job de integracion cinematica aplica estos desplazamientos directamente sobre la posicion y orientacion del agente ya alineado sin sobrecarga de fisicas.

---

## 6. Renderizado Masivo: GPU Animation Instancing, Crossfade y LODs

### 6.1 Textura de Matrices Oseas (`RGBAHalf`)
* Se prescinde de `Animator` y `SkinnedMeshRenderer` para las instancias activas de la horda.
* Cada hueso ocupa 3 pixeles horizontales (las 3 filas superiores de su transformacion afin $4 \times 4$).
* Dimensiones de textura: $\text{Ancho} = \text{Huesos} \times 3$, $\text{Alto} = \sum \text{Fotogramas de todos los clips horneados}$.

### 6.2 Vertex-Space Crossfade y Rotacion Procedural de Cabeza (HLSL)
En el Vertex Shader de URP:
* Se reciben buffers de propiedades instanciadas: `_AnimParamsA`, `_AnimParamsB`, `_BlendParams` y `_HeadAimAngles`.
* Se introducen desfases de cuadro y variaciones deterministas por indice de instancia para mitigar la sincronizacion artificial del movimiento.
* Si `_BlendParams.x > 0.001`, se calculan ambas matrices en espacio de vertices y se interpola con `lerp(posA, posB, blendWeight)`.
* Si `boneIndex == HEAD_BONE_INDEX`, se premultiplica la matriz osea correspondiente por la matriz Yaw/Pitch calculada hacia el objetivo.

### 6.3 Frustum Culling y Clasificacion de LODs en Burst (`FrustumDistanceCullJob`)
* Clasifica las instancias contra los 6 planos del frustum de la camara activa.
* **LOD 0 (0 - 25 m):** Malla de alta resolucion, crossfade de animacion y rotacion procedural de cabeza activos.
* **LOD 1 (25 - 60 m):** Malla simplificada sin crossfade ni orientacion de cabeza.
* **Culled (> 60 m o fuera de camara):** Se excluye del despacho a la GPU (`0 draw calls`), preservando la ejecucion de navegacion e IA en los Jobs.
* Compatible con Unity 2022.3 LTS (`Graphics.DrawMeshInstanced`) y Unity 6 (`#if UNITY_6000_0_OR_NEWER` con `Graphics.RenderMeshInstanced`).

---

## 7. Sistema Hibrido de Ragdolls y Ciclo de Vida de Cadaveres

### 7.1 Pool Fisico y Transferencia Exacta de Pose (`RagdollPool`)
Al morir una entidad en rango cercano (LOD 0):
1. Se excluye del lote de GPU Instancing y se solicita una instancia fisica a `RagdollPool` (capacidad tipica de 25-30 instancias prealojadas).
2. Se reconstruye la matriz en espacio de modelo empleando las inversas precalculadas:
   $$M_{\text{model}} = M_{\text{skin}} \times M_{\text{bindpose}}^{-1}$$
3. Se configuran los `Rigidbody` con `isKinematic = true`, se trasladan las posiciones y rotaciones de mundo a cada hueso, se reactiva `isKinematic = false`, se transfiere la velocidad lineal previa y se aplica el impulso correspondiente en el hueso impactado mediante `AddForceAtPosition`.

### 7.2 Congelacion Estatica (`BakeMesh`) y Cola FIFO de Disolucion (`CorpseLifecycleRegistry`)
1. Cuando `RagdollSleepObserver` constata que todos los componentes `Rigidbody` alcanzaron `IsSleeping()`, se congela la geometria con `skinnedRenderer.BakeMesh(bakedStaticMesh)` y se retorna el cuerpo fisico a `RagdollPool`.
2. El cadaver estatico pasa a la cola de `CorpseLifecycleRegistry` (limite configurable, por ejemplo 80 cuerpos).
3. Al superarse el cupo maximo, el cadaver mas antiguo activa el material con el shader `Custom/URP_CorpseDissolve`, modulando `_DissolveAmount` de `0` a `1` mediante `MaterialPropertyBlock` (con pass `ShadowCaster` habilitado).
4. Al culminar la disolucion, se destruye explicitamente la instancia `Mesh` creada para evitar fugas de memoria en VRAM y se recicla el contenedor.

---

## 8. Herramientas de Editor (`ZombieBakeWizardWindow`)
Ubicada en `Tools/UZE/Zombie Baker Wizard`:
* Procesa el Prefab con `SkinnedMeshRenderer`, las mallas de `LOD0`/`LOD1`, clips de animacion y parametros cinematizados.
* Instancia el modelo en escena oculta, muestrea las transformaciones a frecuencia fija (ej. 30 FPS), calcula las matrices oseas normalizadas respecto a `bindPoses`, extrae traslaciones y rotaciones para `RootMotionData`, serializa la textura en formato `.asset` y genera el asset de configuracion `ZombieArchetypeAsset` para su uso por `HordeSimulationDispatcher`.
