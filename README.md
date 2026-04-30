## Cambios por revisar

### Movimiento del barco

| Archivo | Ubicacion | Descripcion |
|---------|-----------|-------------|
| `BoatControl.cs` | `Assets/WaterRippleShader Eldvmo/Scripts/BoatControl.cs` | Movimiento del barco con teclado (W/S avanzar, A/D girar), velocidad, velocidad de rotacion e inclinacion visual (roll/pitch) al moverse |
| `BoatFloatControl.cs` | `Assets/WaterRippleShader Eldvmo/Scripts/BoatFloatControl.cs` | Flotacion del barco sobre el agua usando raycasts al plano de agua y efecto de ondas (ripple) |

### Cambios en el panel de control

| Archivo | Ubicacion | Descripcion |
|---------|-----------|-------------|
| `PanelController.cs` | `Assets/Panel/PanelController.cs` | Se eliminaron las zonas: Agua Norte/Sur/Este/Oeste y Cuadrantes NE/NO/SE/SO. Solo quedan las 6 zonas de Atras y Todo el oceano |
| `PanelController.cs` | `Assets/Panel/PanelController.cs` | Se elimino el texto de estado del panel (statusLabel y descripcion) para que el titulo CAMPO VECTORIAL aparezca en la parte superior |
| `PanelUISetup.cs` | `Assets/Editor/PanelUISetup.cs` | Se ocultan el statusLabel y el descriptionLabel en el layout del prefab |

---

# Aplicacion Vectores

Aplicacion interactiva de **visualizacion de campos vectoriales** en 3D desarrollada en Unity con soporte para **Realidad Virtual (XR)** y modo escritorio FPS.

---

## Indice

- [Descripcion](#descripcion)
- [Arquitectura](#arquitectura)
- [Estructura del Proyecto](#estructura-del-proyecto)
- [Componentes Principales](#componentes-principales)
- [Campos Vectoriales](#campos-vectoriales)
- [Controles](#controles)
- [Instalacion](#instalacion)
- [Tecnologias](#tecnologias)

---

## Descripcion

La aplicacion genera y anima campos vectoriales en un plano 2D proyectado en el espacio 3D. El usuario puede explorar la escena en primera persona (escritorio) o en realidad virtual, e interactuar con un panel de control para cambiar la formula del campo, la cantidad de vectores y otras opciones en tiempo real.

---

## Arquitectura

```mermaid
graph TB
    subgraph Entrada["Entrada del usuario"]
        KB[Teclado / Raton]
        VR[Controladores VR / XR]
    end

    subgraph Camara["Modo de camara"]
        FPS[FPSCameraController]
        XRO[XR Origin - XR Rig]
    end

    subgraph UI["Panel de control World Space"]
        PC[PanelController]
        DD1[Dropdown: Cantidad]
        DD2[Dropdown: Formula]
        BTN1[Boton: Generar]
        BTN2[Boton: Reset]
        BTN3[Boton: Eliminar]
        STATUS[Etiqueta de estado]
    end

    subgraph Core["Nucleo - Campo Vectorial"]
        VFM[VectorFieldManager]
        GRID[BuildGrid2D]
        EVAL[EvaluateFormula]
        PLACE[PlaceArrow]
        ANIM[AnimateArrows]
    end

    subgraph Render["Renderizado"]
        PREFAB[Prefab Flecha]
        SIMPLE[Flecha Simple Procedural]
        DOT[Punto - magnitud cero]
    end

    KB --> FPS
    VR --> XRO
    FPS --> PC
    XRO --> PC

    PC --> DD1
    PC --> DD2
    PC --> BTN1 --> VFM
    PC --> BTN2 --> VFM
    PC --> BTN3 --> VFM
    PC --> STATUS

    VFM --> GRID --> EVAL --> PLACE
    PLACE --> PREFAB
    PLACE --> SIMPLE
    PLACE --> DOT
    VFM --> ANIM
```

---

## Estructura del Proyecto

```mermaid
graph LR
    ROOT[AplicacionVectores/]

    ROOT --> ASSETS[Assets/]
    ROOT --> PKG[Packages/]
    ROOT --> PS[ProjectSettings/]

    ASSETS --> SCENES[Scenes/\nSampleScene]
    ASSETS --> VF[VectorField/\nVectorFieldManager.cs]
    ASSETS --> PANEL[Panel/\nPanelController.cs]
    ASSETS --> FPS_C[FPSCameraController.cs]
    ASSETS --> PREFABS[Prefabs/\nXR Origin, Panel]
    ASSETS --> PLAYER[Player/]
    ASSETS --> XR_F[XR/\nXR Settings]
    ASSETS --> XRI[XRI/\nXR Interaction]
    ASSETS --> MODELS[2D models/]
    ASSETS --> FLECHA[Flecha/\nPrefab Flecha]
    ASSETS --> SAMPLES[Samples/]
```

---

## Componentes Principales

### Diagrama de clases

```mermaid
classDiagram
    class VectorFieldManager {
        +GameObject arrowPrefab
        +int vectorCount
        +float fieldRadius
        +float arrowScale
        +FieldFormula formula
        +bool useTarget
        +Vector3 target
        +float animSpeed
        +float animAmplitude
        +GenerateField()
        +ResetField()
        +DeleteField()
        -AnimateArrows()
        -BuildGrid2D(n, radius) List~Vector2~
        -EvaluateFormula(p) Vector2
        -PlaceArrow(pos, dir, count)
        -CreateSimpleArrow(pos, rot) GameObject
        -DetectPrefabOrientation()
        -ClearArrows()
    }

    class FieldFormula {
        <<enumeration>>
        RadialOutward
        RadialInward
        RotationXY
        Gravitational
        Saddle
        Constant
        Whirlpool
        Spiral
        TargetPoint
    }

    class PanelController {
        +VectorFieldManager fieldManager
        +TMP_Dropdown dropCount
        +TMP_Dropdown dropFormula
        +Button btnGenerate
        +Button btnReset
        +Button btnDelete
        +TextMeshProUGUI statusLabel
        -OnGenerate()
        -OnReset()
        -OnDelete()
        -OnFormulaChanged(idx)
        -InitDropdowns()
        -SetStatus(msg)
    }

    class FPSCameraController {
        +float moveSpeed
        +float sprintMultiplier
        +float verticalSpeed
        +float mouseSensitivity
        -HandleMovement()
        -HandleRotation()
        -IsPointerOverUI() bool
    }

    class ArrowAnimData {
        +Vector3 basePos
        +Vector3 moveDir
        +float offset
    }

    VectorFieldManager --> FieldFormula : usa
    VectorFieldManager --> ArrowAnimData : contiene
    PanelController --> VectorFieldManager : controla
```

---

## Campos Vectoriales

La aplicacion incluye **9 formulas** de campos vectoriales visualizables:

| # | Nombre | Formula | Descripcion |
|---|--------|---------|-------------|
| 0 | **Radial Saliente** | F = (x, y) | Vectores apuntan hacia afuera del origen |
| 1 | **Radial Entrante** | F = (-x, -y) | Vectores convergen hacia el origen |
| 2 | **Rotacion XY** | F = (-y, x) | Campo rotacional puro (sin divergencia) |
| 3 | **Gravitacional** | F = -r / \|r\|² | Simula campo gravitacional / electrico |
| 4 | **Silla** | F = (x, -y) | Campo hiperbolico tipo punto de silla |
| 5 | **Constante** | F = (1, 0) | Flujo uniforme en una direccion |
| 6 | **Torbellino** | F = (-y, x) / \|r\|² | Rotacion con singularidad en el origen |
| 7 | **Espiral** | F = (x-y, x+y) | Combinacion de expansion y rotacion |
| 8 | **Punto Objetivo** | F = (T - p) / \|T - p\| | Vectores apuntan hacia un punto dado |

### Ciclo de generacion del campo

```mermaid
sequenceDiagram
    participant U as Usuario
    participant PC as PanelController
    participant VFM as VectorFieldManager

    U->>PC: Selecciona formula / cantidad
    PC->>VFM: formula = FieldFormula.X
    PC->>VFM: vectorCount = N
    PC->>VFM: GenerateField()
    VFM->>VFM: ClearArrows()
    VFM->>VFM: DetectPrefabOrientation()
    VFM->>VFM: BuildGrid2D(N, radius)
    loop Para cada punto del grid
        VFM->>VFM: EvaluateFormula(p)
        VFM->>VFM: PlaceArrow(pos, dir)
    end
    VFM-->>PC: Campo generado
    PC-->>U: Estado: "OK N vec -> formula"

    loop Cada frame (Update)
        VFM->>VFM: AnimateArrows()
    end
```

---

## Controles

### Modo Escritorio (FPS)

| Tecla / Accion | Funcion |
|----------------|---------|
| `W A S D` / Flechas | Mover la camara |
| `Click Derecho` + Raton | Rotar la vista |
| `E` / `Espacio` | Subir |
| `Q` / `Ctrl Izq` | Bajar |
| `Shift` | Sprint (velocidad x2.5) |
| `Scroll` (fuera del panel) | Subir / bajar camara |
| `Click Izquierdo` | Interactuar con el panel |

### Modo VR (XR)

| Accion | Funcion |
|--------|---------|
| Movimiento fisico | Desplazamiento en la escena |
| Controlador Ray | Apuntar e interactuar con el panel |
| Trigger | Seleccionar opciones del panel |

### Panel de Control

| Control | Opciones |
|---------|---------|
| Dropdown Cantidad | 100 a 2000 vectores (paso de 100) |
| Dropdown Formula | 8 formulas predefinidas |
| Boton Generar | Genera el campo con la configuracion actual |
| Boton Reset | Reinicia a configuracion por defecto (Radial Out, 100) |
| Boton Eliminar | Elimina todos los vectores de la escena |

---

## Instalacion

### Requisitos

- **Unity 6** (recomendado) o Unity 2022 LTS
- **XR Interaction Toolkit** (incluido en Packages)
- **TextMesh Pro** (incluido en Assets)
- Dispositivo VR opcional (compatible con OpenXR): Meta Quest, HTC Vive, etc.

### Pasos

1. Clonar o descargar el repositorio:
   ```bash
   git clone https://github.com/BurbanoValentina/AplicacionVectores.git
   ```
2. Abrir Unity Hub y seleccionar **Open Project**.
3. Navegar hasta la carpeta `AplicacionVectores/` y confirmar.
4. En Unity, abrir la escena principal:
   ```
   Assets/Scenes/SampleScene.unity
   ```
5. Presionar **Play** para ejecutar en modo escritorio.
6. Para VR, configurar el dispositivo en **Edit > Project Settings > XR Plug-in Management**.

---

## Tecnologias

| Tecnologia | Uso |
|------------|-----|
| **Unity** | Motor de juego y renderizado |
| **C#** | Logica de la aplicacion |
| **XR Interaction Toolkit** | Soporte para realidad virtual |
| **TextMesh Pro** | UI de texto de alta calidad |
| **Universal Render Pipeline (URP)** | Pipeline de renderizado |
| **OpenXR** | Compatibilidad con multiples dispositivos VR |
| **ProBuilder** | Modelado de la flecha 3D |

---

