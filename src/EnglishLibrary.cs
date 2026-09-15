using System;
using System.Collections.Generic;
namespace Polymita {
    // Translate only labels shipped by earlier versions. User recipe payloads remain untouched.
    public static class EnglishLibrary {
        static readonly Dictionary<string,string> Titles = new Dictionary<string,string> {
            {"Datos y árboles","Data and trees"},{"Planos","Planes"},{"Puntos","Points"},{"Curvas","Curves"},
            {"Parámetros y listas","Parameters and lists"},{"Árboles y lógica","Trees and logic"},
            {"Números y dominios","Numbers and domains"},{"Planos y vectores","Planes and vectors"},
            {"Superficies","Surfaces"},{"Mallas","Meshes"},{"Visualización","Display"},
            {"Transformaciones","Transforms"},{"Herramientas","Tools"},{"Adicionales","Additional"},{"Operaciones","Operations"}
        };
        public static bool Apply(ShelfLibrary library) {
            if(library.EnglishRevision>=1) return false;
            foreach(var section in library.Sections) {
                string title; if(Titles.TryGetValue(section.Title,out title)) section.Title=title;
                foreach(var item in section.Items) {
                    if(item.Name=="Panel · lista 0–1") item.Name="Panel · list 0–1";
                    if(item.Name=="Conectar selección · Alt+W") item.Name="Connect selection · Alt+W";
                    if(item.Name=="Duplicar selección · Alt+Q") item.Name="Duplicate selection · Alt+Q";
                    if(item.Notes=="Conecta de izquierda a derecha, por orden de puertos.") item.Notes="Connects left to right, in port order.";
                    if(item.Notes=="Copia componentes y grupos completos a un espacio libre a la derecha.") item.Notes="Copies components and complete groups to free space on the right.";
                    if(item.Notes!=null) item.Notes=item.Notes.Replace(" · configuración guardada"," · saved configuration").Replace("Panel de QuickConnection · ","QuickConnection panel · ");
                }
            }
            library.EnglishRevision=1; return true;
        }
    }
}

