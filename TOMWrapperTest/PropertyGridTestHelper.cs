using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using TabularEditor.TOMWrapper;

namespace TOMWrapperTest
{
    public static class PropertyGridTestHelper
    {
        public static FluentPropertyGridTest PropertyGridTest(this ITabularNamedObject obj)
        {
            var propertyGrid = new PropertyGrid();
            propertyGrid.SelectedObject = obj;

            var item = propertyGrid.SelectedGridItem;
            while(item.Parent != null) { item = item.Parent; }

            var result = new FluentPropertyGridTest(obj);

            var itemStack = new Stack<GridItem>();
            itemStack.Push(item);

            while(itemStack.Count > 0)
            {
                item = itemStack.Pop();
                if (item.Expandable) item.Expanded = true;
                foreach(GridItem subItem in item.GridItems)
                {
                    itemStack.Push(subItem);
                }
                if (item.PropertyDescriptor != null)
                {
                    result.Add(item.GetPath(), item.PropertyDescriptor);
                }
                else
                {
                    result.Categories.Add(item.Label, item.GridItems.OfType<GridItem>().Where(gi => gi.PropertyDescriptor != null).Select(gi => gi.PropertyDescriptor.Name).ToList());
                }
            }

            return result;
        }

        public static string GetPath(this GridItem item)
        {
            var pathItems = new List<string>();
            pathItems.Add(item.PropertyDescriptor.Name);
            while(item.Parent != null)
            {
                item = item.Parent;
                if(item.PropertyDescriptor != null)
                    pathItems.Add(item.PropertyDescriptor.Name);
            }
            return string.Join("\\", pathItems.OfType<string>().Reverse());
        }
    }
}
