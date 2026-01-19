using System.Windows;
using System.Windows.Controls;
using SGA_Desktop.Models;

namespace SGA_Desktop.Helpers
{
    public class TraspasoYAjusteTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? TraspasoDtoTemplate { get; set; }
        public DataTemplate? AjusteDtoTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item == null)
                return null;

            // Determinar el tipo real del objeto en tiempo de ejecución
            var itemType = item.GetType();

            if (itemType == typeof(TraspasoDto) || itemType.IsSubclassOf(typeof(TraspasoDto)))
            {
                // Añadir badge de fuente para traspasos en la vista combinada
                if (item is TraspasoDto traspaso)
                {
                    traspaso.Fuente = "SGA_Actual";
                }
                return TraspasoDtoTemplate;
            }

            if (itemType == typeof(AjusteDto) || itemType.IsSubclassOf(typeof(AjusteDto)))
            {
                return AjusteDtoTemplate;
            }

            // Si no coincide con ningún tipo conocido, retornar null (WPF usará el template por defecto)
            return null;
        }
    }
}

