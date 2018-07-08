﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Kexi.View
{
    //https://stackoverflow.com/questions/1959856/data-binding-the-textblock-inlines?utm_medium=organic&utm_source=google_rich_qa&utm_campaign=google_rich_qa
    public static class TextBlockExtensions
    {
        public static IEnumerable<Inline> GetBindableInlines ( DependencyObject obj )
        {
            return (IEnumerable<Inline>) obj.GetValue ( BindableInlinesProperty );
        }

        public static void SetBindableInlines ( DependencyObject obj, IEnumerable<Inline> value )
        {
            obj.SetValue ( BindableInlinesProperty, value );
        }

        public static readonly DependencyProperty BindableInlinesProperty =
            DependencyProperty.RegisterAttached ( "BindableInlines", typeof ( IEnumerable<Inline> ), typeof ( TextBlockExtensions ), new PropertyMetadata ( null, OnBindableInlinesChanged ) );

        private static void OnBindableInlinesChanged ( DependencyObject d, DependencyPropertyChangedEventArgs e )
        {
            var Target = d as TextBlock;

            if ( Target != null )
            {
                Target.Inlines.Clear ();
                Target.Inlines.AddRange ( (System.Collections.IEnumerable) e.NewValue );
            }
        }
    }
}
