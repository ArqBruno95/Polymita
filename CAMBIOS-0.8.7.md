# Polymita 0.8.7 — selección en los puertos, color de wires y ayudas de Rhino

## Cambios

### 1. Volver a seleccionar un componente clicando sus inputs y outputs

El problema no era un radio de acción dibujado alrededor de los puertos, sino un
gesto que se quedaba con la pulsación. En Grasshopper, presionar sobre la zona de
un input o de un output **inicia un wire**: si sueltas sin moverte, Grasshopper
cancela ese wire y esa misma liberación es la que selecciona el componente.

Polymita respondía a esa liberación con `GH_ObjectResponse.Ignore` para dejar el
wire enganchado al cursor y poder conectar con dos clics. Grasshopper nunca
recibía la liberación, así que no seleccionaba nada, y el clic siguiente se lo
comía el wire que seguía esperando. De ahí la sensación de un anillo muerto
alrededor de cada puerto y de que solo el icono respondiera.

- Una liberación que llega después de una pulsación **que no se ha movido** se
  devuelve tal cual a Grasshopper. Selección, arrastre y doble clic vuelven a ser
  exactamente los nativos.
- El alcance con el que se descarta la paleta al soltar un wire de verdad vuelve
  a ser el radio de agarre propio de Grasshopper (10 unidades de canvas), en
  lugar del alcance ampliado en píxeles de pantalla.
- Se elimina también la sustitución del gesto en `RespondToMouseDown`, que
  convertía cada pulsación izquierda en una liberación.
- La paleta al soltar un wire sobre el canvas vacío o sobre un grupo **se
  mantiene**: es el arrastre completo, no el clic.

### 2. El plugin ya no toca el color de las wires

Se retira por completo el subsistema de resaltado, no se desactiva:

- `WireStyles.SetHighlight` y los colores de `GH_Skin` que guardaba y restauraba
  (`wire_selected_a`, `wire_selected_b`).
- Las preferencias `Highlight` y `SelectedArgb` de `ToolboxSettings`.
- La casilla «Highlight selected connections» y el botón «Selection color…» de la
  pestaña Wires.
- Las llamadas en el arranque y al abrir la caja de herramientas.

Polymita dibuja **el recorrido** de una wire y nada más. El color, seleccionado o
no, es decisión exclusiva de Grasshopper. La comprobación nativa correspondiente
se ha reescrito para verificar justo eso: activar y reiniciar todo el subsistema
de wires deja los colores del skin exactamente como estaban.

### 3. Las ayudas de dibujo de Rhino dentro del viewport de Grasshopper

Con las posiciones que tienen en Rhino.

**Debajo de los selectores de vista y de estilo de display**, la línea de comandos:

- El **prompt en vivo** de Rhino, tal cual (`RhinoApp.CommandPrompt`).
- El **historial** del comando (`RhinoApp.CommandHistoryWindowText`), en un cuadro
  de solo lectura que se desplaza al final.
- Las **opciones que Rhino ofrece en ese momento**, como botones. Rhino escribe
  sus opciones dentro de un paréntesis al final del prompt, así que al llamar
  `Line` aparecen BothSides, Normal, Angled, Vertical, FourPoint, Bisector,
  Perpendicular, Tangent y Extension. Pulsar un botón envía exactamente lo mismo
  que escribir el nombre de la opción; una opción con valor (`Radius=5`) se envía
  por su nombre, que es lo que hace Rhino al clicarla.
- La caja de entrada acepta lo mismo que la línea de comandos de Rhino: un
  comando, una opción, un número o una coordenada. Escape cancela.

**Abajo**, las ayudas de modelado:

- Barra de **referencias a objeto**: End, Near, Point, Mid, Cen, Int, Perp, Tan,
  Quad, Knot, Vertex, Project y Disable. Clic izquierdo alterna; **clic derecho
  deja esa referencia sola**, como en Rhino.
- **Barra de estado**: Grid Snap, Ortho, Planar, Osnap, SmartTrack y Gumball.
- **Lector de distancia** desde el último punto elegido en esa vista, en las
  unidades y con la precisión del documento (`3.604 m`). Mientras un comando está
  en marcha se mide desde el punto elegido hasta el cursor sobre el plano de
  construcción; cuando el comando produce una curva, se muestra su longitud real.

Ningún interruptor guarda un estado propio: todos leen y escriben la preferencia
de Rhino (`ModelAidSettings`, `SmartTrackSettings`), de modo que uno cambiado
desde la barra de estado de Rhino o con una tecla F aparece aquí, y al revés. El
panel consulta a Rhino cada 200 ms porque Rhino no publica ningún evento ni para
el prompt ni para estas ayudas; solo escribe cuando el valor difiere de verdad.

## Validación

Compilación de la versión 0.8.7.0 sin errores ni advertencias, con los paquetes
NuGet del SDK de Rhino 8.34.26223.11001.

- 34 comprobaciones de biblioteca y funciones básicas (5 nuevas, sobre la lectura
  de las opciones del prompt de Rhino, incluido el prompt real de `Line`).
- 39 comprobaciones de almacenamiento, migración y disposición de operaciones.
- Las 13 suites compilan, incluida la nueva `ViewportAidsTests`.

**Pendiente de comprobar dentro de Rhino**, porque aquí no hay Rhino:

- `tests/run-viewport-aids.py` ejecuta `ViewportAidsTests` sobre las ayudas de
  modelado: nombres y orden de las dos barras, ida y vuelta de cada referencia a
  la preferencia de Rhino, clic derecho para dejar una sola, Disable y el formato
  de la distancia. La suite guarda los valores de Rhino al empezar y los restaura
  al terminar.
- La selección clicando inputs y outputs, el color de las wires y el
  comportamiento de la línea de comandos solo se pueden confirmar con Grasshopper
  abierto.

## Instalación

1. Cierra todas las ventanas de Rhino.
2. Sustituye el `Polymita.gha` anterior por el nuevo en la carpeta Libraries de
   Grasshopper. Deja una sola versión activa, también en las subcarpetas.
3. Abre Rhino y Grasshopper. El menú Polymita debe indicar la versión 0.8.7.
