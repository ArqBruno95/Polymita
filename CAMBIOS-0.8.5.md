# Revisión y corrección de Polymita 0.8.5

## Entrega

Plugin compilado para Grasshopper de Rhino 8 / Windows, con código fuente, instalador, iconos y pruebas. La base es el ZIP 0.8.4 adjunto. Se mantienen el GUID del plugin, el formato de biblioteca y los identificadores de comandos.

## Cómo está organizado

`Plugin.cs` conecta el plugin con Grasshopper, sus menús, barra de herramientas y eventos. `Palette`, `Editor`, `Library`, `Recipes` e `Insertion` gestionan favoritos, bibliotecas y componentes con configuración guardada. `CanvasGestures` controla el corte y la alineación. `CanvasOperations` implementa los comandos explícitos de selección. El resto aporta estilos de wires, etiquetas, buscador/profiler y el viewport nativo de Rhino.

La revisión se concentra en los cuatro problemas solicitados. Las recetas, el catálogo de componentes, las operaciones, el buscador, las etiquetas y el viewport conservan sus implementaciones, salvo las referencias al nombre del producto.

## 1. Arrastrar + Alt

**Causa encontrada:** 0.8.4 evitaba capturar el arrastre si Alt estaba pulsado al iniciar el clic, pero sustituía los arrastres normales por una interacción propia. Esta consumía las teclas y no ejecutaba la respuesta nativa cuando Alt se pulsaba después de empezar a mover.

**Cambio:** la interacción de alineación hereda ahora de `GH_DragInteraction`. Al pulsar Alt durante un arrastre restaura las posiciones iniciales y entrega el movimiento y los eventos de teclado y ratón a la implementación de Grasshopper. La copia, su previsualización, los cambios de modo con Alt y su historial los realiza Grasshopper. No se incorpora una rutina propia de duplicación para este gesto.

El comando explícito **Alt+Q** sigue disponible, porque es una función independiente. Los manejadores de atajos de Polymita tampoco ejecutan comandos mientras hay una interacción o gesto de ratón activo.

**Comportamiento previsto:** empezar a arrastrar y después pulsar Alt. A partir de ese momento, ese arrastre utiliza también el snap nativo. Al terminar, el siguiente arrastre normal vuelve a utilizar el snap de Polymita.

## 2. Anclaje a puertos y wire cercano al cursor

**Causas encontradas:** ya había un factor 4, aplicado solamente a una coordenada. Los bordes podían ganar la comparación y quitar el anclaje al puerto. Además, el código escogía un grip cercano al cursor pero lo comparaba con los extremos remotos de todos los wires de la selección, incluidos wires de otros puertos.

**Cambio:**

- Bordes y centros de objetos: radio de **8 píxeles de pantalla**.
- Vértices input/output: radio de **32 píxeles**, en ambos ejes, independiente del zoom.
- Dentro de ese radio, el puerto tiene prioridad sobre el borde. Al salir, se libera.
- El punto donde se inicia el arrastre elige el grip conectado más cercano del componente agarrado. El grip conserva exclusivamente los extremos remotos de sus propias conexiones.
- El anclaje horizontal utiliza la altura de ese grip, de modo que pueda enderezarse ese wire aunque el componente tenga varios inputs/outputs.
- La elección se mantiene durante el arrastre. Los puertos sin wire no sustituyen a la conexión seleccionada.
- Si el objeto agarrado no tiene conexiones externas, sus ejes centrales pueden alinearse con los inputs/outputs de los objetos de referencia.

La lógica nueva de correspondencia entre grips y conexiones está en `DragAnchors.cs`.

## 3. Iconos de operaciones

Se eliminan las dos imágenes de `Brand.cs`, sus referencias y los cuatro PNG de operation connect/duplicate. La hoja de iconos contiene ahora seis familias.

Los comandos se conservan en el menú y los atajos. Los favoritos de operaciones existentes se presentan como filas de texto para conservar su orden, identidad y utilidad. Una biblioteca nueva deja de añadir automáticamente la sección Operations.

## 4. Nombre y almacenamiento

Nombre, namespace C#, proyecto, metadatos, diálogos, exportaciones, nuevos chunks de recetas y extracción de dependencias utilizan Polymita.

```text
%APPDATA%\Grasshopper\Polymita\
    library.polymita.json
    toolbox.json
    runtimes\...
```

`SettingsStorage.cs` migra los datos anteriores si falta el archivo correspondiente en la carpeta nueva. Las copias conservan los bytes de la biblioteca, sus recetas, GUID, configuración de puertos, nombres personalizados y backups. Un archivo Polymita existente no se sobrescribe durante la migración.

**Excepción deliberada:** las grafías anteriores se conservan únicamente como entradas de compatibilidad del migrador y el instalador, y en sus pruebas. Son necesarias para localizar los datos y ensamblados ya instalados. Las carpetas originales quedan como respaldo; las nuevas escrituras utilizan Polymita. Tampoco se reemplazan palabras dentro de recetas o nombres personalizados del usuario.

El instalador se corrige para utilizar `release/Polymita.gha`, que es la ubicación del binario en el ZIP. Archiva las versiones anteriores conocidas y deja un único ensamblado activo.

## Verificación realizada

| Comprobación | Resultado |
|---|---|
| Compilación con SDK local de Rhino/Grasshopper 8.34.26223.11001 | Correcta, sin advertencias |
| Biblioteca, recetas serializadas, preferencias y layout base | 29 comprobaciones correctas |
| Migración, backups, identidad y filas de operaciones | 39 comprobaciones correctas |
| Wires, hit testing, preferencias y traducción de biblioteca | 65 comprobaciones correctas |
| Geometría del snap a zoom 0,5 / 1 / 2 y herencia nativa | 16 comprobaciones correctas |
| Instalador en carpeta temporal | Binario idéntico, dos versiones previas archivadas, un único GHA activo |
| Binario | Versión 0.8.5.0 y tres runtimes Harmony incrustados |
| Iconos | Hoja regenerada y revisada visualmente |

Total: **149 comprobaciones locales**, además de la prueba del instalador y la revisión del paquete. Se actualizó una expectativa obsoleta del test de herramientas: la propia 0.8.4 ya activaba los wires adaptativos por defecto; el código productivo de ese ajuste se conserva.

### Validación pendiente en Rhino

Las pruebas nativas de gesto, grupos, Undo/Redo, conexiones y recetas se han ampliado y compilado, pero **no se han ejecutado en una sesión funcional de Rhino durante esta revisión**. La conexión por interfaz caducó sin autorización; el intento de host aislado tampoco alcanzó el script. Por tanto, no se afirma que el gesto real haya sido validado visualmente.

Para ejecutar la suite: compilar con `tests/prepare-runtime.ps1 -Name Polymita.Regression085` y abrir `tests/run-regression-085.py` mediante RunPythonScript en Rhino. El script utiliza definiciones temporales y restaura la definición original. Sus resultados quedan en `test-output/`.

Conviene comprobar especialmente: arrastrar → Alt → soltar; pulsar Alt dos veces; Escape; copia de grupos y deshacer; y agarrar un componente de varios puertos por alturas distintas para confirmar la sensación de anclaje.

## Instalación

Cerrar Rhino, extraer el ZIP y ejecutar `Install-Polymita.ps1`. Alternativamente, copiar `release/Polymita.gha` a la carpeta Libraries de Polymita, sustituyendo la versión anterior y evitando dejar otra versión activa en una subcarpeta. Reiniciar Rhino y Grasshopper.

## Referencias de la revisión

Se inspeccionó el ensamblado de Grasshopper instalado para comprobar qué métodos gestionan Alt, su liberación y el arrastre. Como contraste, la [documentación de interacción de McNeel](https://developer.rhino3d.com/api/grasshopper/html/M_Grasshopper_GUI_Canvas_IGH_ResponsiveObject_RespondToMouseMove.htm) explica la entrega exclusiva de eventos a la interacción activa, y una [aclaración de McNeel sobre la copia](https://discourse.mcneel.com/t/copy-paste-not-working/155988) describe pulsar Alt después de empezar a arrastrar.
