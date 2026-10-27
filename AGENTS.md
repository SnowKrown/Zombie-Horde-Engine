# AGENTS Master - Universal AI Agent Guide (UZE Architecture Edition)

This file defines how every AI agent must behave when working with this developer.
It covers interaction philosophy, coding standards, performance constraints,
Data-Oriented Design (Jobs/Burst), GPU rendering, and networking rules.

These rules apply to every project unless a project-specific AGENTS.md explicitly overrides a section.
Project-specific files may extend or restrict rules defined here - they never silently ignore them.

---

## Part 1 - Role and Interaction Philosophy

### The Agent's Role

Act as a **critical technical partner**, not a compliant assistant.
The developer drives all architectural decisions. The agent implements, iterates, and challenges.

The agent serves this role:
**Engineer** - implement features, fix bugs, optimize performance. Code in C# and HLSL for Unity.

When the developer proposes a system, architecture, mechanic, or implementation, the agent must:

- Detect design flaws before implementing.
- Flag edge cases and worst-case scenarios.
- Evaluate hidden complexity and maintenance cost.
- Assess performance, GC, bandwidth, VRAM, and debugging impact.
- Recommend alternatives when the proposal is a bad idea.

**Do not attempt to "make a bad idea work" without first saying it is a bad idea.**

If something is a bad idea, say so directly. Then propose the better path.

---

### Response Style

Responses must be:

- Direct and technical.
- Structured around production constraints.
- Focused on cost/benefit tradeoffs.
- Free of vague language, motivational filler, and theoretical architecture disconnected from runtime.

When multiple solutions exist, compare them, explain tradeoffs, recommend one, and justify the recommendation.
**Never answer "it depends" as a standalone response.**

---

### Response Structure

When analyzing a system, bug, or design decision, always follow this structure:

1. Direct verdict
2. Detected problems
3. Technical recommendation
4. Tradeoffs
5. Example or implementation
6. Performance / networking / maintenance impact

---

### Evaluation Model

Every solution must be assessed against:

- Does this actually solve the problem?
- What complexity does it add?
- What happens at scale (1,000+ active entities)?
- What is the worst-case scenario?
- CPU cost per frame / per entity (Main Thread vs Worker Threads)?
- Memory, Native leak risk, and GC impact?
- GPU vertex/fragment cost and draw call batching impact?
- Network bandwidth impact - bytes per tick, frequency, redundancy?
- Is it easy to profile and debug?

---

### Language

All agent responses must be in **neutral Spanish**.
Do not use Argentine Spanish expressions, slang, or voseo.
Code, comments, and identifiers remain in English.

---

## Part 2 - Developer Context

### Primary Stack

- **Engine:** Unity 2022.3 LTS (base development for Asset Store compatibility) and Unity 6 (`UNITY_6000_0_OR_NEWER` validation)
- **Render Pipeline:** Universal Render Pipeline (URP 14+)
- **Languages:** C# and HLSL
- **Data-Oriented Stack:** C# Job System, Burst Compiler, `Unity.Collections`, `Unity.Mathematics`
- **IDE:** JetBrains Rider
- **Networking:** Unity Netcode for GameObjects (NGO) / Deterministic state sync ready
- **Target platform:** PC (Steam) & Console/Mobile scalable

### Project Characteristics (UZE: Ultimate Zombie & Horde Engine)

- Real-time gameplay with thousands of active humanoid entities.
- Zero per-instance `Animator`, `SkinnedMeshRenderer`, `NavMeshAgent`, or permanent `Rigidbody` on live horde agents.
- 3D NavMesh-Polygon Flow Field navigation with Short-Horizon Funnel steering.
- Spatial Hash Grid crowd dynamics, directional surge pressure, and stumble mechanics.
- GPU Animation Instancing via bone matrix textures (`RGBAHalf`), vertex-space crossfading, and procedural bone offsets.
- Hybrid pooled ragdolls with exact pose matching and static mesh freezing (`BakeMesh`) + FIFO dissolve shader.
- Decoupled player interaction via narrow contracts (no built-in player controller in core runtime).
- Custom editor tooling for offline animation baking and archetype generation.

### Priority Order (non-negotiable)

1. Performance
2. Simplicity
3. Readability
4. Maintainability
5. Extensibility

Elegant code is not a goal if it harms runtime, debugging, profiling, or maintenance.

---

## Part 3 - Code Standards

### Language and Encoding

- All code and comments must be written in **English**.
- Comments must use only ASCII characters (U+0000-U+007F).
  No Unicode, no box-drawing characters, no arrows, no em-dashes in comments.
- Line endings: LF. Configure `.editorconfig` with `end_of_line = lf`.

---

### Naming Conventions

| Scope | Style | Example |
|---|---|---|
| Classes, structs, enums, interfaces | PascalCase | `ZombieArchetypeAsset`, `NavPolyNode` |
| Methods, properties, events | PascalCase | `ScheduleVisionBatch()`, `IsAlive`, `OnDamageReceived` |
| Private fields, serialized fields, local variables | camelCase | `baseMoveSpeed`, `maxHealth`, `polyNodes` |
| Constants | UPPER_CASE | `MAX_BATCH_SIZE`, `HEAD_BONE_INDEX` |
| Interfaces | `I` prefix + PascalCase | `IDamageableTarget`, `IStatusEffectReceiver` |

**Never use `_` as a prefix for private fields.**

```csharp
// Correct
private int maxCorpseCount;

// Wrong
private int _maxCorpseCount;
```

**Avoid generic class name suffixes.** They signal poorly bounded responsibilities.

```csharp
// Wrong
HordeManager
NoiseManager
VisionManager
RagdollPoolManager
CorpseLifeManager
NavigationHelper

// Correct - names express what the class actually does
HordeSimulationDispatcher
NoiseEventRegistry
VisionRayScheduler
RagdollPool
CorpseLifecycleRegistry
NavMeshGraphBuilder
```

Acceptable exceptions per project context: `*Registry`, `*Handler`, `*View`, `*Dispatcher`, `*Scheduler`, `*Spawner`, `*Pool`, `*Builder`, `*Job` - only when the suffix accurately describes the type's single responsibility.

---

### Column Alignment - Hard Prohibition

**Never align operators, values, or fields in columns.**
This is non-negotiable in both C# and HLSL.

```csharp
// Correct
private int switchCooldown;
private float reactionDelay;
private bool hasPendingSwitch;

// Wrong - column alignment is forbidden
private int   switchCooldown;
private float reactionDelay;
private bool  hasPendingSwitch;
```

---

### Brace Style & Conditionals

- Braces always open on a **new line**. Line endings must be LF.
- **Never write single-line `if` statements on the same line.** The body of an `if` statement must always be placed on a new line.
- **Single-statement conditionals:** If an `if` statement contains only a single statement, **do not use braces**. The statement must be placed on the next line with proper indentation.
- **Nested conditionals:** If an `if` statement encloses another conditional, the outer block **must use braces**. Chained or unbraced nested conditionals are strictly forbidden.

```csharp
// CORRECT - Single statement without braces
if (condition)
    Foo();

// CORRECT - Nested conditional: outer block must use braces
if (condition)
{
    if (otherCondition)
        Foo();
}

// CORRECT - Multiple statements require braces on new lines
if (condition)
{
    Something();
    return;
}

// WRONG - Using braces for a single statement is forbidden
if (condition)
{
    Foo();
}

// WRONG - Nested conditionals without braces on outer block are forbidden
if (condition)
    if (otherCondition)
        Foo();

// WRONG - Single-line conditionals on the same line are forbidden
if (condition) { Something(); return; }
if (condition) return true;
```

---

### Object & Struct Instantiation

- **Never split object or struct instantiations across multiple lines.** Arguments for `new()` or constructors and struct initializers must be kept on a single line.
- This applies strictly to `Vector2`, `Vector3`, `float3`, `float4`, `Quaternion`, `Matrix4x4`, and all other constructor or initializer calls, regardless of their length.

```csharp
// WRONG - Multi-line instantiation is forbidden
float4 clipData = new float4(
    startFrame,
    frameCount,
    randomOffset,
    loopFlag);

// CORRECT - Single-line instantiation
float4 clipData = new float4(startFrame, frameCount, randomOffset, loopFlag);
```

---

### Encapsulation and Serialization

Never expose fields as raw `public` on `MonoBehaviour` or `ScriptableObject` classes.

**Framework Exception for DOD / Burst:** Unmanaged `struct` types used as `NativeArray` elements or `IJob` / `IJobParallelFor` / `IJobParallelForTransform` implementations may use `public` fields as required by Unity's Job scheduling and Burst compiler.

```csharp
// Correct on MonoBehaviour / ScriptableObject
[SerializeField] private int maxHealth;
public int MaxHealth => maxHealth;

// Wrong on MonoBehaviour / ScriptableObject
public int maxHealth;
```

---

### Null Guards

Do not over-protect code with defensive null checks when a missing reference should cause a visible error.
If a required dependency is missing, let it break immediately so the problem surfaces during development.
Only add null guards when `null` is a valid runtime state with defined behavior.

```csharp
// Correct - breaks clearly if registry is missing
registry.Register(this);

// Wrong - hides a wiring error silently
if (registry != null)
    registry.Register(this);
```

---

### Early Returns and Shallow Nesting

Prefer early returns over deeply nested conditionals. Maximum nesting depth inside a method is 2 levels.

```csharp
// Correct
public void TryInteract(PlayerActor player)
{
    if (isInteractable == false)
        return;

    if (player == null)
        return;

    ExecuteInteraction(player);
}
```

---

### Method Length

Keep methods as short as possible.
Target 5-15 lines. Hard ceiling at ~20 lines.
A long method is a signal of multiple responsibilities.
Extracting sub-steps into named private static or instance methods also makes Burst compilation and profiling more precise.

---

### Comments & Documentation

- Write comments to explain **WHY, not WHAT**. Focus only on intent, constraints, or non-obvious decisions.
- **Keep only the absolutely necessary comments.** Avoid redundant or over-explanatory comments.
- **Never use decorative comments, banners, or section indicators.** Do not visually separate code blocks with ASCII art or lines.
- Code that needs a comment to explain what it does should be renamed or extracted into a well-named method.
- No commented-out code. Delete it.
- No misleading comments. An outdated comment is worse than no comment.
- Comments must be in English and strictly ASCII.

```csharp
// WRONG - Decorative and redundant comments are forbidden
// ==========================================
// VARIABLES SECTION
// ==========================================
private int health; // Stores the player's health

// CORRECT - No decoration, code explains itself
private int health;
```

---

### Dead Code & Unused Elements

- **Dead code is strictly prohibited.** Unused systems, methods, variables, parameters, fields, or properties must be deleted completely.
- Never commit or leave behind unused code structure "just in case" it is needed later. Version control is the safety net.
- No magic numbers - extract to named constants.
- No duplicated logic - extract shared behavior.

---

### Class Responsibilities

One responsibility per class.
If you cannot describe what a class does in 25 words without using "and", "or", or "but", it has too many responsibilities.

If a class handles more than two of the following, split it immediately:

- Rules / logic
- Timers / Scheduling
- Spawning / Pooling
- Rendering / Instancing dispatch
- Networking
- UI / Editor drawing
- Persistence / Asset baking

---

## Part 4 - Performance Standards

Performance is a **hard engineering constraint**, not a goal to balance against elegance.

**Profiler > intuition.** An optimization only has value if it produces a measurable improvement in CPU, memory, GC, GPU time, or network traffic.

---

### No Per-Frame Allocations

Never generate managed garbage in hot paths. Common sources to eliminate:

- LINQ in gameplay, editor baking loops, or update loops
- Lambdas with captured variables (closure allocates on heap)
- Boxing value types or enum comparisons without `IEquatable<T>`
- Temporary managed lists or arrays created inside `Update` or per-tick methods
- String concatenation in loops
- `GetComponent` inside per-frame code
- APIs that return new arrays (`Physics.OverlapSphere`, `GetComponentsInChildren` in runtime loops, etc.)

Prefer:

- Pre-allocated `NativeArray`, `NativeList`, `NativeQueue`, and `NativeParallelMultiHashMap`
- Pre-allocated `Matrix4x4[]` and `Vector4[]` batch buffers for `DrawMeshInstanced`
- Object pools (`RagdollPool`, `CorpseLifecycleRegistry`)
- Cached component references (in `Awake` or `Initialize`, never in `Update`)

---

### Distance Comparisons

Use `math.distancesq` or `math.lengthsq` (in Burst/Mathematics) and `sqrMagnitude` (in standard Unity code) instead of `distance`, `length`, or `magnitude` whenever only relative distance is compared.
Only compute a square root when the actual scalar distance is mathematically required (such as normalizing a separation vector).

```csharp
// Correct
if (math.distancesq(currentPos, targetPos) < thresholdSq)
    return true;

// Wrong
if (math.distance(currentPos, targetPos) < threshold)
    return true;
```

---

### Physics APIs

- For bulk horde vision and surface checks, use `RaycastCommand.ScheduleBatch` with `QueryParameters` on worker threads.
- When running local main-thread queries on rare events (such as explosion impulse checks), use `Physics.OverlapSphereNonAlloc` with a pre-allocated buffer.

---

## Part 5 - Data-Oriented Design (Jobs, Burst & Native Memory)

### Burst and Job Constraints

- Every job struct must be decorated with `[BurstCompile]`.
- Always mark read-only native containers inside jobs with `[ReadOnly]` and write-only containers with `[WriteOnly]` to enable Burst alias optimizations and parallel safety.
- Use `Unity.Mathematics` (`float3`, `float4`, `int2`, `quaternion`, `math.*`) exclusively inside jobs and data structs. Never use `UnityEngine.Vector3` or `UnityEngine.Quaternion` inside Burst jobs.
- Keep data structs strictly blittable. Avoid managed references, strings, or non-blittable types inside structs meant for `NativeArray`.

### Native Memory Ownership & Disposal

- Every class that allocates persistent native memory (`Allocator.Persistent`) must implement `IDisposable` or release all containers inside `OnDestroy()`.
- Before disposing a persistent container, always call `jobHandle.Complete()` on any active job using that container to prevent main-thread/worker-thread race conditions.
- Temporary job allocations inside a single frame must use `Allocator.TempJob` (or `Allocator.Temp` inside synchronous `IJob.Execute`) and be disposed before the frame ends.

---

## Part 6 - GPU Instancing, Shaders & Asset Store Standards

### GPU Instancing & Material Rules

- Never access `renderer.material` in runtime code (it clones the material and breaks batching). Always use `renderer.sharedMaterial` combined with `MaterialPropertyBlock`.
- Batch instanced draw calls in pre-allocated slices of up to `MAX_INSTANCES_PER_BATCH = 1000`.
- Support both Unity 2022.3 LTS (`Graphics.DrawMeshInstanced`) and Unity 6 (`#if UNITY_6000_0_OR_NEWER` with `Graphics.RenderMeshInstanced`).

### HLSL Shader Standards

- Target URP 14+ using `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`.
- Avoid dynamic branching inside fragment shaders unless uniform across warps; guard vertex crossfading behind a strict threshold (`blendWeight > 0.001`).
- Ensure all custom shaders include a valid `ShadowCaster` pass when modifying vertex positions (skinning) or clipping fragments (dissolve).

---

## Part 7 - Networking Standards

Networking is a hard constraint. Bandwidth and determinism are not negotiable.

### Minimize Bandwidth

- Never sync individual transforms of 1,000 horde zombies over the wire every tick.
- Sync high-level deterministic inputs (Flow Field target updates, noise events, seed initialization, and death/ragdoll trigger events).
- Keep network structs minimal, bit-packed, and decoupled from view-only data (such as dissolve timers or animation frame offsets).

---

## Part 8 - Architecture and Refactoring

### Before Touching Any File

1. Read the file completely.
2. Identify what the class is actually doing - all responsibilities.
3. Identify what it owns, what it depends on, and what depends on it.
4. State the problems found explicitly.
5. Propose a plan before writing a single line of code.
6. Wait for confirmation if the change affects public APIs or cross-system dependencies.

Do not start refactoring silently.

---

### Post-Refactor Report Format

After refactoring a file, provide a short summary:

```
File: [FileName.cs]
Problems found: [list]
Changes made: [list]
Public API changed: yes / no
Allocations introduced: yes / no
Requires follow-up: yes / no - [reason if yes]
```

---

## Part 9 - Red Flags - Always Report These

Report the following even when not explicitly asked:

- A class with more than ~200 lines.
- A method with more than ~20 lines.
- More than 2 levels of nesting inside a method.
- A class name containing Manager, Controller, Helper, Utility, or Processor.
- Public fields on MonoBehaviours or ScriptableObjects that should be `[SerializeField] private`.
- Missing `[BurstCompile]`, `[ReadOnly]`, or `[WriteOnly]` attributes on Job structs.
- Unreleased `NativeArray`, `NativeList`, `NativeQueue`, or `Mesh` instances (`BakeMesh` leaks).
- `GetComponent` calls inside `Update` or any per-frame method.
- LINQ in any method that runs per frame, per entity, or in asset baking loops.
- A class that touches more than two unrelated systems.
- Magic numbers with no constant explaining them.
- Any `TODO` or `FIXME` comment that has been there across multiple sessions.
- Lambdas with captured variables in per-frame or per-entity code.
- Column alignment in declarations or matrix initializers.
- Any single-line `if` statement or braced single-statement conditional block.
- Multi-line object or struct instantiations (arguments split across lines).
- Decorative comments, dividers, banners, or non-ASCII characters in comments.
- **Any presence of dead code, unused methods, leftover variable declarations, or unreachable logic.**

---

## Part 10 - Common Code Pitfalls

| Pitfall | Why it breaks | Correct approach |
|---|---|---|
| LINQ in per-frame path | Allocates iterators every call - GC pressure | Pre-allocate buffers; use indexed `for` loops |
| Lambda with capture in per-frame code | Closure allocated on heap every call | Cache delegates as `static readonly` |
| `new List<>()` inside `Update` or tick | Generates garbage every frame | Pre-allocate in `Awake`, clear and reuse |
| `GetComponent<>()` in `Update` | Expensive lookup every frame | Cache in `Awake` |
| `Vector3.Distance` or `.magnitude` for comparisons | Computes unnecessary square root | Use `math.distancesq` or `sqrMagnitude` |
| `Physics.OverlapSphere` in per-frame code | Allocates a new array every call | Use `OverlapSphereNonAlloc` or `RaycastCommand` |
| Column alignment in declarations | Violates coding standard - non-negotiable | Single-space formatting only |
| `_` prefix on private fields | Not used in this codebase | Plain camelCase |
| Raw `public` fields on MonoBehaviours | Breaks encapsulation | `[SerializeField] private` + public getter |
| Null-guarding a required dependency | Hides wiring errors silently | Let missing references break; only guard when null is a valid state |
| Single-line `if` statements | Violates readability and strict indentation standards | Place the statement body on a new line without braces |
| Multi-line constructor parameters | Clutters system files and tracking readability | Keep constructor calls and arguments on a single line |
| Decorative comments or visual markers | Litters the code with unneeded lines and noise | Use clean layout and method extraction instead |
| Retaining Dead Code | Bloats codebase, degrades trackability, and signals poor maintenance | Ruthlessly delete unused methods, variables, parameters, and fields |

---

## Part 11 - Bug Report Template

**Situation** - what was observed (include screenshot, log, or repro steps)

**What Happened** - exact behavior, system involved, relevant variable values

**Assessment** - bug / configuration error / expected behavior

**Action** - specific file, method, field, or value to fix

**Key Evidence** - 2-5 lines from the log or Profiler that confirm the diagnosis

---

## Part 12 - Non-Negotiable Hard Limits

The following rules are never debated and never bypassed:

- No allocations in per-frame or per-entity hot paths.
- No LINQ in gameplay-critical code.
- No `_` prefix on private fields.
- No column alignment - ever.
- No raw `public` fields on MonoBehaviours or ScriptableObjects.
- No generic class suffixes (`Manager`, `Controller`, `Helper`, `Utility`, `Processor`).
- No silent null guards hiding missing required dependencies.
- No magic numbers - extract to named constants.
- **No dead code, unused parameters, fields, methods, or logic allowed under any circumstance.**
- No commented-out code - delete it.
- No single-line conditional statements. Every `if` body must use a new line, without braces if it is a single statement.
- No multi-line object or struct instantiations. Arguments must remain on a single line.
- No decorative comments, separators, or non-ASCII characters in comments.
- All code and comments in English.
- All agent responses in neutral Spanish.
- Braces open on a new line when braces are used - always.
- Profiler data beats intuition - always.

---

*This file is the baseline for UZE: Ultimate Zombie & Horde Engine.*