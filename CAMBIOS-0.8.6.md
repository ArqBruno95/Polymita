# Polymita 0.8.6 — correcciones de Canvas

## Cambios

- **Selección:** el arrastre conserva la activación diferida de Grasshopper. Un clic o un pequeño movimiento de hasta dos píxeles no se convierte en un arrastre activo, por lo que la liberación del botón llega al componente. Polymita solo amplía una interacción de arrastre iniciada por Grasshopper.
- **Fin del arrastre:** se elimina la captura manual del ratón. Si se pierde la captura o llega un movimiento sin el botón izquierdo, se libera la interacción. El último desplazamiento se conserva con Deshacer; la cancelación restaura la posición. La limpieza admite llamadas repetidas.
- **Wires verdes:** después de soltar el ratón o las teclas, se programa un repintado completo del Canvas, una vez terminado el procesamiento nativo de selección. El color se calcula a partir de la selección actual, sin borrar selecciones ni cambiar los colores elegidos por el usuario.
- **Selección múltiple:** los bordes, el centro y los puertos del componente bajo el cursor sirven de referencia para desplazar todo el conjunto. Se conservan las distancias entre sus miembros.
- **Grupos:** agarrar el borde de un grupo utiliza su contorno y centro como referencia, sin que los puertos de un miembro desplacen ese snap. Se admiten grupos anidados y grupos con anotaciones. Los grupos que contienen objetos en movimiento quedan excluidos de las referencias estacionarias.
- Se mantienen las mejoras de 0.8.5: snaps de puertos con alcance cuatro veces mayor, elección del wire según el punto de agarre, Alt nativo, herramientas, recetas, biblioteca, preferencias, nombres Polymita y eliminación de los dos iconos de operaciones.

## Validación

Compilación de la versión 0.8.6.0 sin errores ni advertencias, con el SDK de Rhino 8.34.26223.11001 instalado.

- 29 comprobaciones de biblioteca y funciones básicas.
- 39 comprobaciones de almacenamiento, migración y disposición de operaciones.
- 65 comprobaciones de herramientas, preferencias y geometría de wires.
- 16 comprobaciones locales de geometría del snap.
- **26 comprobaciones nuevas ejecutadas dentro de Rhino**, sobre un Canvas aislado: clics repetidos, liberación del arrastre, pérdida de captura, Deshacer, referencias de selección múltiple, grupos anidados con anotaciones y repintado.
- **48 comprobaciones de regresión ejecutadas dentro de Rhino**: varios inputs/outputs, wire más cercano, alcance del snap, copia nativa con Alt, repetición de Alt, Escape, Deshacer/Rehacer y conservación de comandos. Este conjunto incluye nuevamente las 16 comprobaciones de geometría.

La prueba gráfica compara la región de wires antes de seleccionar, después de seleccionar todo y después de deseleccionar. Los wires recuperaron el color inicial; se permite una diferencia de hasta 8 niveles RGB para la animación y el antialiasing de la cuadrícula nativa.

Las pruebas de Canvas usan documentos temporales en un Canvas independiente; no sustituyen la definición abierta ni registran una segunda versión del plugin. Los resultados no equivalen a una prueba exhaustiva con todas las extensiones de terceros o todas las definiciones posibles. Las suites adicionales de operaciones, recetas y corte se compilan, pero no se han vuelto a ejecutar en Rhino en esta revisión.

## Instalación

1. Cierra todas las ventanas de Rhino.
2. Sustituye el `Polymita.gha` anterior por el nuevo en la carpeta Libraries de Grasshopper. Deja una sola versión activa, también en las subcarpetas.
3. Reinicia Rhino y Grasshopper.

El archivo es autónomo e incorpora las dependencias Harmony necesarias. El ZIP contiene también código fuente, pruebas y el instalador. No es necesario borrar la biblioteca ni las preferencias.

## Repetir las pruebas

Compilar con `tests/prepare-runtime.ps1 -Name Polymita.CanvasTests` y ejecutar `tests/run-regression-086.py` con RunPythonScript de Rhino. El lanzador espera a que se hayan soltado las teclas del comando. Los resultados se guardan en `test-output/`.

## SHA-256 del GHA

`2723161862145b97e6fd5289267908193d37ed81e44e3ec363bdd52c9fb44407`
