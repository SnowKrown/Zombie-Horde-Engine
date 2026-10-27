# Prompt Inicial de Desarrollo - UZE: Ultimate Zombie & Horde Engine (v1.0)

Actúa como un Ingeniero Principal de Software y Desarrollador de Motores en Unity experto en Data-Oriented Design (DOD), C# Job System, Burst Compiler, Universal Render Pipeline (URP), programación de Shaders en HLSL y arquitectura de herramientas para la Unity Asset Store.

Vamos a desarrollar desde cero la versión 1.0 del plugin **UZE: Ultimate Zombie & Horde Engine**, basándonos en la arquitectura definida en el archivo adjunto `UZE_TECHNICAL_GUIDE.md`.

## ⚠️ REGLA CRÍTICA OBLIGATORIA ANTES DE EMPEZAR
**La guía técnica solamente es un ejemplo de cómo irá el código, todo el código seguirá los estandares puestos en AGENTS.md, por lo que necesito que antes de continuar actualices el código de la guía según AGENTS.md**

---

## Flujo de Trabajo que Debes Seguir

### Fase 0: Auditoría y Alineación con `AGENTS.md` (Hacer Ahora Mismo)
1. Lee íntegramente el archivo `AGENTS.md` del espacio de trabajo y extrae todas las reglas de estilo, convenciones de nomenclatura, estructura de carpetas, arquitectura, manejo de memoria, comentarios y estándares de calidad.
2. Lee `UZE_TECHNICAL_GUIDE.md`.
3. Reescribe y actualiza `UZE_TECHNICAL_GUIDE.md` para que todos sus fragmentos de código, nombres de tipos, interfaces, estructuras, métodos, namespaces y organización de archivos cumplan **al 100%** con lo estipulado en `AGENTS.md`.
4. Muéstrame un resumen de las adaptaciones aplicadas sobre la guía técnica y **espera mi confirmación** antes de generar los archivos de código de producción.

### Fase 1: Estructura del Paquete y Contratos Base (Tras confirmación)
* Configurar la estructura de carpetas modular compatible con UPM / Asset Store y sus respectivos Assembly Definitions (`.asmdef`) para `Runtime`, `Editor` y `Samples~`.
* Implementar las interfaces desacopladas de interacción con el jugador (`IStatusEffectReceiver`, `IGrabbableTarget`, `IDamageableTarget`) y los tipos de datos base (`ZombieArchetypeSO`, enums de estado y estructuras blittable).

### Fase 2: Navegación 3D Multicapa y Funnel en Burst
* Implementar el constructor del grafo topológico sobre el NavMesh 3D (`NavMeshGraphBuilder`, `NavPolyNode`, `NavPolyEdge`).
* Implementar el Job de inundación Dijkstra (`GeneratePolyFlowFieldWithEdgesJob`), el rastreador de polígono actual (`TrackCurrentPolygonJob`) y el algoritmo de suavizado de trayectoria `Short-Horizon Funnel` (`FunnelAlgorithm`).

### Fase 3: Dinámica de Horda, Spatial Hash y Estampida
* Implementar el `SpatialHash` con `NativeParallelMultiHashMap`.
* Implementar el Job de movimiento unificado con separación lateral filtrada, repulsión de esquinas, cálculo de presión direccional (`rearPressure` / `frontResistance`), modulación dinámica de velocidad y sistema de tropiezos/desequilibrio (`EvaluateHordeCrowdStressJob`).

### Fase 4: Percepción Sensorial y Ataques con Root Motion
* Implementar el `NoiseManager` centralizado en Jobs.
* Implementar el `ZombieVisionManager` con Time-Slicing de 4 frames, filtro de cono por producto punto y `RaycastCommand.ScheduleBatch`.
* Implementar soporte para Root Motion horneado en ataques especiales de embestida (`LungeAttack`).

### Fase 5: Renderizado por GPU Instancing, Shaders HLSL y Editor Baker
* Escribir el shader HLSL para URP con decodificación de matrices óseas desde textura `RGBAHalf`, Vertex-Space Crossfade e inyección de rotación procedural para la cabeza (`LookAt`).
* Implementar el `FrustumDistanceCullJob` y el despachador de lotes `LOD 0` y `LOD 1` (con directivas `#if UNITY_6000_0_OR_NEWER` y retrocompatibilidad para Unity 2022.3 LTS).
* Construir la ventana de editor `ZombieBakeWizardWindow` para automatizar el horneado de animaciones y generación de `ZombieArchetypeSO`.

### Fase 6: Sistema Híbrido de Ragdolls y Reciclaje de Cadáveres
* Implementar el `RagdollPoolManager` con transferencia exacta de pose ($M_{\text{model}} = M_{\text{skin}} \times M_{\text{bindpose}}^{-1}$) e inercia física.
* Implementar el congelador de mallas (`BakeMesh`), el gestor FIFO de cadáveres y el shader HLSL `URP_CorpseDissolve` con soporte para `ShadowCaster`.

---

Comienza ejecutando únicamente la **Fase 0**: lee `AGENTS.md`, actualiza `UZE_TECHNICAL_GUIDE.md` acorde a dichos estándares y preséntame los cambios realizados.