using System.Windows;
using System.Windows.Controls;
using SGA_Desktop.Models;

namespace SGA_Desktop.Helpers
{
    public class TraspasoCombinadoTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? TraspasoDtoTemplate { get; set; }
        public DataTemplate? TraspasoStorageControlDtoTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item == null)
                return null;

            // Determinar el tipo real del objeto en tiempo de ejecución
            var itemType = item.GetType();

            if (itemType == typeof(TraspasoDto) || itemType.IsSubclassOf(typeof(TraspasoDto)))
            {
                return TraspasoDtoTemplate;
            }

            if (itemType == typeof(TraspasoStorageControlDto) || itemType.IsSubclassOf(typeof(TraspasoStorageControlDto)))
            {
                return TraspasoStorageControlDtoTemplate;
            }

            // Si no coincide con ningún tipo conocido, retornar null (WPF usará el template por defecto)
            return null;
        }
    }
}

