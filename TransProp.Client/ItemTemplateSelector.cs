using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace TransProp.Client
{
    public class ItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate CommentTemplate { get; set; }
        public DataTemplate ValueTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            CompViewModelItem vm = item as CompViewModelItem;
            if (vm != null)
            {
                return vm.IsComment ? CommentTemplate : ValueTemplate;
            }
            return base.SelectTemplate(item, container);
        }


    }
}
