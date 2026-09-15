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

## Revisión de la ventana del viewport

Sobre la captura anotada:

- **Fuera lo tachado.** Desaparece la línea con la palabra «Command», que solo
  repetía lo que ya decía la caja, y desaparecen los botones **Run** y **Fit**.
  Enter envía, igual que en Rhino. El encuadre conjunto de la vista de
  Grasshopper y la geometría de Rhino que hacía «Fit» sigue ejecutándose al
  cambiar la dirección de vista; si lo quieres a mano en algún sitio, dímelo.
- **Todo lo nuevo, plegado.** La barra de Command con su historial y la fila de
  ayudas de modelado empiezan **ocultas**. Cada una tiene su flecha: `▸ Command`
  debajo de los selectores de vista y display, y `▸ Osnap, Ortho, distance` en el
  borde inferior. Al pulsarla se despliega y **se queda así** hasta que la vuelvas
  a pulsar, también al reiniciar Grasshopper.
- **Coste plegado:** una flecha de unos 18 px en cada extremo. El resto es
  viewport.
- **Una sola fila, por temas.** Las ayudas van una al lado de otra, con una línea
  fina de separación entre grupos: referencias a objeto │ interruptores de estado
  │ distancia. Se reparten en varias líneas solo si el panel es estrecho.
- **Botones sin recortar.** Los rótulos aparecían cortados («Disab», «SmartTra»)
  porque `AutoSize` sobre un `CheckBox` con apariencia de botón mide el texto por
  debajo. Ahora cada botón recibe el ancho que su propio texto necesita, con
  cuerpo de 8 pt y 20 px de alto.
- **El prompt va delante de la caja**, como lo escribe Rhino, y sin la lista de
  opciones entre paréntesis: esas son los botones.
- Mientras la fila está plegada no se observa el ratón de Rhino ni se consulta el
  prompt: el panel solo pregunta por lo que está a la vista.

## Corrección: las flechas no desplegaban nada

Las dos flechas aparecían, pero al pulsarlas no salía ninguna franja. El fallo
estaba en el manejador del clic:

```csharp
internal Disclosure(string caption, bool open, ...) {
 this.open = open;
 Click += delegate { open = !open; ... };   // invierte el PARÁMETRO
}
```

Dentro del `delegate`, `open` se refiere al **argumento del constructor**
capturado por el closure, no al campo `this.open`. Cada clic invertía esa copia,
el campo seguía en `false`, la flecha nunca cambiaba de dibujo y el panel volvía
a poner `Visible = false` sobre la franja. El argumento se llama ahora `start`, y
el cambio de estado vive en un método `Toggle()` que escribe el campo.

De paso se ha aplanado la disposición. Cada franja estaba metida con su flecha en
una tabla propia con alto automático, es decir tres niveles de alto negociado
entre la franja y el panel. Ahora la barra de comandos, la fila de ayudas y las
dos flechas se acoplan directamente al panel, el mismo mecanismo que ya usa la
barra VIEW/DISPLAY; un control oculto simplemente no ocupa sitio. Al desplegar o
plegar, el panel rehace su disposición y reposiciona la ventana de Rhino en el
acto, en vez de esperar al siguiente tic de 100 ms.

`ViewportAidsTests` incluye ahora cuatro comprobaciones sobre la flecha: que
empieza plegada, que un clic la abre y avisa una sola vez, que otro clic la
vuelve a plegar, y que una franja dejada abierta se dibuja abierta al volver.
