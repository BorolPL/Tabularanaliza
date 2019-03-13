﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using TabularEditor.PropertyGridUI;

namespace TabularEditor.TOMWrapper
{
    public class CalculationGroupAttribute: ITabularObjectContainer, ITabularNamedObject, ITabularTableObject
    {
        private DataColumn Column;
        internal CalculationGroupAttribute(DataColumn column)
        {
            Column = column;
        }

        [DisplayName("Description")]
        [Category("Basic"), Description(@"The description of the column, visible to developers at design time and to administrators in management tools, such as SQL Server Management Studio."), IntelliSense(@"The description of the column, visible to developers at design time and to administrators in management tools, such as SQL Server Management Studio.")]
        [Editor(typeof(System.ComponentModel.Design.MultilineStringEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string Description { get => Column.Description; set => Column.Description = value; }
        [DisplayName("Display Folder")]
        [Category("Basic"), Description(@"Defines the display folder for the column, for use by clients."), IntelliSense(@"Defines the display folder for the column, for use by clients.")]
        [Editor(typeof(CustomDialogEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string DisplayFolder { get => Column.DisplayFolder; set => Column.DisplayFolder = value; }
        [DisplayName("Hidden")]
        [Category("Basic"), Description(@"A boolean value that indicates whether a column is treated as hidden by client visualization tools. True if the column is treated as hidden; otherwise false."), IntelliSense(@"A boolean value that indicates whether a column is treated as hidden by client visualization tools. True if the column is treated as hidden; otherwise false.")]
        public bool IsHidden { get => Column.IsHidden; set => Column.IsHidden = value; }
        [DisplayName("Extended Properties"), NoMultiselect, Category("Translations and Perspectives"), Description("The collection of Extended Properties on the current Column."), Editor(typeof(ExtendedPropertyCollectionEditor), typeof(UITypeEditor))]
        public ExtendedPropertyCollection ExtendedProperties => Column.ExtendedProperties;
        [Browsable(true), NoMultiselect, Category("Translations and Perspectives"), Description("The collection of Annotations on the current Column."), Editor(typeof(AnnotationCollectionEditor), typeof(UITypeEditor))]
        public AnnotationCollection Annotations => Column.Annotations;
        [Browsable(true), DisplayName("Translated Names"), Description("Shows all translated names of the current Column."), Category("Translations and Perspectives")]
        public TranslationIndexer TranslatedNames => Column.TranslatedNames;
        [Browsable(true), DisplayName("Translated Descriptions"), Description("Shows all translated descriptions of the current Column."), Category("Translations and Perspectives")]
        public TranslationIndexer TranslatedDescriptions => Column.TranslatedDescriptions;
        [Browsable(true), DisplayName("Translated Display Folders"), Description("Shows all translated Display Folders of the current Column."), Category("Translations and Perspectives")]
        public TranslationIndexer TranslatedDisplayFolders => Column.TranslatedDisplayFolders;
        [DisplayName("Object Type")]
        public string ObjectTypeName => "Calculation Group Attribute";
        [Browsable(true), DisplayName("Shown in Perspective"), Description("Provides an easy way to include or exclude the current Column from the perspectives of the model."), Category("Translations and Perspectives")]
        public PerspectiveIndexer InPerspective => Column.InPerspective;

        [Category("Basic"), NoMultiselect()]
        [Description("The name of this object. Warning: Changing the name can break formula logic, if Automatic Formula Fix-up is disabled.")]
        [IntelliSense("The name of this object. Warning: Changing the name can break formula logic, if Automatic Formula Fix-up is disabled.")]
        public string Name { get => Column.Name; set => Column.Name = value; }

        [Browsable(false)]
        public int MetadataIndex => Column.MetadataIndex;

        [Browsable(false)]
        public ObjectType ObjectType => ObjectType.CalculationGroupAttribute;

        [Browsable(false)]
        public Model Model => Column.Model;

        [Browsable(false)]
        public bool IsRemoved => Column.IsRemoved;

        [Browsable(false)]
        public Table Table => Column.Table;
        [Browsable(false)]
        public CalculationGroupTable CalculationGroup => Column.Table as CalculationGroupTable;

        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                ((ITabularNamedObject)Column).PropertyChanged += value;
            }

            remove
            {
                ((ITabularNamedObject)Column).PropertyChanged -= value;
            }
        }

        public bool CanDelete()
        {
            return false;
        }

        public bool CanDelete(out string message)
        {
            message = null;
            return false;
        }

        public bool CanEditName()
        {
            return true;
        }

        public void Delete()
        {
        }

        public IEnumerable<ITabularNamedObject> GetChildren()
        {
            return CalculationGroup.CalculationItems;
        }
    }
}