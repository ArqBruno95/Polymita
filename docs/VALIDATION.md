# Verificación de Polymita 0.2.0

Windows, Rhino 8.34.26223.11001, runtime .NET 8.0.14. 11–12 de septiembre de 2026.

## Automatización

- 25 comprobaciones: persistencia y recuperación de biblioteca, orden, recetas, búsquedas, validación, revisión de migración, doble Shift frente a pulsación simple/larga/autorrepetida/lenta/cancelada, y cuadrícula de 197 iconos sin solapamientos.
- 26 comprobaciones dentro de Rhino: captura nativa, IDs únicos, Panel, Flatten, Simplify, conexiones en ambas direcciones, preservación de fuentes previas, Undo/Redo, rechazo de puertos inválidos, migración que conserva favoritos previos, ausencia de duplicación al reiniciar, respeto de eliminaciones posteriores, y creación sin cables.
- Catálogo: 197 entradas solicitadas resueltas por GUID; las procedentes de imágenes se contrastaron con la configuración local de QuickConnection. Biblioteca final: 203 accesos, incluidos los favoritos previos.

## Interacción comprobada

- Doble Shift abre la cuadrícula sobre el lienzo; un clic inserta Panel con sus datos, sin cables. Ctrl+Z elimina esa inserción.
- Todos los 203 iconos se ven simultáneamente en la pantalla comprobada, con títulos de sección y nombres en tooltips.
- Un cable desde el output de Merge abre la misma cuadrícula en modo Conectar.
- El botón derecho ofrece las entradas del componente y la opción Insertar sin cable.
- La ventana de edición sigue permitiendo capturar, reordenar, renombrar, añadir y eliminar favoritos y secciones.

## Corrección durante las pruebas

Rhino cerró inesperadamente al probar el menú contextual en una compilación intermedia. El registro .NET identificó ObjectDisposedException sobre ContextMenuStrip dentro de ToolStripManager.ModalMenuFilter. La versión corregida difiere Dispose hasta el siguiente ciclo de mensajes, después de que termine el cierre y se entregue Click. El cierre no afectó a la biblioteca guardada; no se encontró una copia de recuperación reciente de la definición sin guardar que estaba abierta.

El doble Shift utiliza WH_KEYBOARD solo en el hilo de interfaz de Rhino: IMessageFilter no recibe las teclas bajo el bucle nativo de este host. El manejador se desinstala al apagar Polymita; las teclas siguen llegando a Grasshopper. Se limita a Grasshopper activo con el cursor sobre el lienzo y no registra texto ni usa un gancho global.

## Límites

Los GUID e iconos están verificados, pero no se han evaluado todas las operaciones geométricas de los plugins de terceros. Se requiere tenerlos instalados. Las pruebas se han ejecutado en Rhino 8 para Windows; no cubren macOS, Rhino 7 ni todos los tamaños y escalados de pantalla. Si la biblioteca excede la pantalla, la cuadrícula permite desplazamiento.

## Revisión final, 12 de septiembre

Se repitieron las 26 comprobaciones de integración en la compilación corregida. Se abrió y cerró su menú contextual sin el fallo anterior y, después, un clic insertó Merge con Flatten en ambos inputs. Rhino siguió respondiendo. La selección exacta de puertos se verifica en las pruebas de integración; la automatización de escritorio devuelve el foco al propietario al pulsar sobre los menús emergentes.

## Polymita 0.4.0 — 2026-09-12

Entorno: Rhino 8.34 / Windows, .NET 8.0.14. Compilación Release sin errores.

- 25 pruebas de biblioteca, migración, copias atómicas, doble Shift y distribución de iconos.
- 38 pruebas de herramientas: geometría ortogonal y angular, extremos, giros de 5/45/90/160 grados, hit-test, persistencia y validación.
- 26 pruebas de integración de recetas e inserción dentro de Rhino.
- 23 pruebas nativas de Finder/Profiler, viewport (siete proyecciones), display mode, parches de wires y restauración del trazado/color.
- 21 pruebas nativas de operaciones: multi-source, no duplicar wires, ciclos, Undo/Redo, grupos anidados, panel personalizado, Flatten/Simplify, color, conexiones internas y externas, posición libre y acciones serializadas.

133 comprobaciones automáticas superadas. Los dos primeros ajustes de prueba fueron la comparación ARGB de colores nativos y desactivar la preferencia persistida antes de capturar el trazado original; no eran defectos en la copia de datos.

Comprobación interactiva: cuatro iconos junto a Sketch; viewport empieza cerrado y alterna desde su botón; solo vista/modo en su barra; ventanas independientes al llamar; biblioteca con botón + Operación; nombres sobre componentes. Alt+Q y Alt+W ejecutados mediante teclado en el canvas de una definición de prueba: copia separada y conexiones visibles.

Limitaciones: las pruebas no garantizan la serialización de todos los componentes de terceros ni rendimiento en definiciones grandes. Se conserva la definición inicial y se retira la definición de prueba del canvas antes de entregar.

### Entrega, 13 de septiembre

Se repitieron las 38 pruebas sobre el ensamblado renombrado y las 70 pruebas nativas (26 + 23 + 21) en la nueva sesión de Rhino. Se confirmó visualmente el título grande del grupo al alejar; su posición final se movió encima del grupo para reducir superposición. El paquete final se instaló en la carpeta de componentes de Grasshopper, con la biblioteca original ampliada a 205 accesos.
