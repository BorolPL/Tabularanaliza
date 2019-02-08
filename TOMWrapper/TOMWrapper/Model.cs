﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace TabularEditor.TOMWrapper
{
    partial class Model: ITabularObjectContainer
    {
        #region Convenient methods
        [IntelliSense("Adds a new perspective to the model."), Tests.GenerateTest()]
        public Perspective AddPerspective(string name = null)
        {
            if (Handler.UsePowerBIGovernance && !PowerBI.PowerBIGovernance.AllowCreate(typeof(Perspective))) return null;

            Handler.BeginUpdate("add perspective");
            var perspective = Perspective.CreateNew(this, name);
            Handler.EndUpdate();
            return perspective;
        }

        [IntelliSense("Adds a new Named Expression to the model."), Tests.GenerateTest(), Tests.CompatibilityLevel(1400)]
        public NamedExpression AddExpression(string name = null, string expression = null)
        {
            if (Handler.UsePowerBIGovernance && !PowerBI.PowerBIGovernance.AllowCreate(typeof(NamedExpression))) return null;

            Handler.BeginUpdate("add shared expression");
            var expr = NamedExpression.CreateNew(this, name);
            Handler.EndUpdate();
            return expr;
        }

        [IntelliSense("Adds a new calculated table to the model."), Tests.GenerateTest()]
        public CalculatedTable AddCalculatedTable(string name = null, string expression = null)
        {
            Handler.BeginUpdate("add calculated table");
            var t = CalculatedTable.CreateNew(this, name, expression);
            Handler.EndUpdate();
            return t;
        }

        [IntelliSense("Adds a new calculation group to the model."), Tests.GenerateTest()]
        public CalculationGroupTable AddCalculationGroup(string name = null, string expression = null)
        {
            Handler.BeginUpdate("add calculation group");
            var t = CalculationGroupTable.CreateNew(this, name);
            Handler.EndUpdate();
            return t;
        }

        internal static Model CreateFromMetadata(TOM.Model metadataObject)
        {
            var obj = new Model(metadataObject);
            obj.Init();
            return obj;
        }

        [IntelliSense("Adds a new table to the model."), Tests.GenerateTest()]
        public Table AddTable(string name = null)
        {
            if (Handler.UsePowerBIGovernance && !PowerBI.PowerBIGovernance.AllowCreate(typeof(Table))) return null;

            Handler.BeginUpdate("add table");
            var t = Table.CreateNew(this, name);
            
            Handler.EndUpdate();
            return t;
        }

        [IntelliSense("Adds a new relationship table to the model."), Tests.GenerateTest()]
        public SingleColumnRelationship AddRelationship()
        {
            Handler.BeginUpdate("add relationship");
            var rel = SingleColumnRelationship.CreateNew(this);
            Handler.EndUpdate();
            return rel;
        }

        [IntelliSense("Adds a new translation to the model."), Tests.GenerateTest("da-DK")]
        public Culture AddTranslation(string cultureId)
        {
            if (Handler.UsePowerBIGovernance && !PowerBI.PowerBIGovernance.AllowCreate(typeof(Culture))) return null;

            Handler.BeginUpdate("add translation");
            var culture = TOMWrapper.Culture.CreateNew(cultureId);
            Handler.EndUpdate();
            return culture;
        }

        [IntelliSense("Adds a new security role to the model."), Tests.GenerateTest()]
        public ModelRole AddRole(string name = null)
        {
            Handler.BeginUpdate("add role");
            var role = ModelRole.CreateNew(this);
            //role.InitRLSIndexer();
            if (!string.IsNullOrEmpty(name)) role.Name = name;
            Handler.EndUpdate();
            return role;
        }

        [IntelliSense("Adds a new data source to the model."), Tests.GenerateTest()]
        public ProviderDataSource AddDataSource(string name = null)
        {
            if (Handler.UsePowerBIGovernance && !PowerBI.PowerBIGovernance.AllowCreate(typeof(ProviderDataSource))) return null;

            Handler.BeginUpdate("add data source");

            var ds = ProviderDataSource.CreateNew(this, name);
            Handler.EndUpdate();
            return ds;
        }

        [IntelliSense("Adds a new strucured data source to the model."), Tests.GenerateTest(), Tests.CompatibilityLevel(1400)]
        public StructuredDataSource AddStructuredDataSource(string name = null)
        {
            if (Handler.CompatibilityLevel < 1400) throw new InvalidOperationException(Messages.CompatibilityError_StructuredDataSource);
            if (Handler.UsePowerBIGovernance && !PowerBI.PowerBIGovernance.AllowCreate(typeof(StructuredDataSource))) return null;

            Handler.BeginUpdate("add data source");
            var ds = StructuredDataSource.CreateNew(this, name);
            Handler.EndUpdate();
            return ds;
        }
        #endregion
        #region Convenient Collections
        /// <summary>
        /// Iterates all hierarchies on all tables of the model.
        /// </summary>
        [Browsable(false),IntelliSense("A collection of every hierarchy across all tables in the model.")]
        public IEnumerable<Hierarchy> AllHierarchies { get { return Tables.SelectMany(t => t.Hierarchies); } }

        /// <summary>
        /// Iterates all columns on all tables of the model.
        /// </summary>
        [Browsable(false),IntelliSense("A collection of every column across all tables in the model.")]
        public IEnumerable<Column> AllColumns { get { return Tables.SelectMany(t => t.Columns); } }

        /// <summary>
        /// Iterates all partitions on all tables of the model.
        /// </summary>
        [Browsable(false), IntelliSense("A collection of every partition across all tables in the model.")]
        public IEnumerable<Partition> AllPartitions { get { return Tables.SelectMany(t => t.Partitions); } }

        /// <summary>
        /// Iterates all measures on all tables of the model.
        /// </summary>
        [Browsable(false), IntelliSense("A collection of every measure across all tables in the model.")]
        public IEnumerable<Measure> AllMeasures { get { return Tables.SelectMany(t => t.Measures); } }

        /// <summary>
        /// Iterates all levels on all hierarchies on all tables of the model.
        /// </summary>
        [Browsable(false), IntelliSense("A collection of every level in every hierarchy across all tables in the model.")]
        public IEnumerable<Level> AllLevels { get { return Tables.SelectMany(t => t.Hierarchies).SelectMany(h => h.Levels); } }
        #endregion

        public IEnumerable<ITabularNamedObject> GetChildren()
        {
            return Groups;
        }

        protected override bool AllowDelete(out string message)
        {
            message = Messages.CannotDeleteObject;
            return false;
        }

        protected override void Init()
        {
            if (Handler.CompatibilityLevel >= 1400 && MetadataObject.DataAccessOptions == null)
                MetadataObject.DataAccessOptions = new TOM.DataAccessOptions();
        }

        protected override bool IsBrowsable(string propertyName)
        {
            switch (propertyName)
            {
                // Compatibility Level 1400 (or newer) features:
                case Properties.EXPRESSIONS:
                case Properties.FASTCOMBINE:
                case Properties.RETURNERRORVALUESASNULL:
                case Properties.LEGACYREDIRECTS:
                case Properties.DEFAULTMEASURE:
                    return Handler.CompatibilityLevel >= 1400;
                case Properties.FORCEUNIQUENAMES:
                    return Handler.CompatibilityLevel >= 1465;
                default:
                    return base.IsBrowsable(propertyName);
            }
        }

        [Browsable(false)]
        public LogicalGroups Groups { get { return LogicalGroups.Singleton; } }

        [Category("Basic")]
        [IntelliSense("Gets the database object of the model.")]
        public Database Database { get; internal set; }

        [Category("Data Access Options"),DisplayName("Enable Fast Combine")]
        public bool FastCombine {
            get { return MetadataObject.DataAccessOptions.FastCombine; } 
            set { SetValue(FastCombine, value, v => MetadataObject.DataAccessOptions.FastCombine = (bool)v, Properties.FASTCOMBINE); }
        }
        private bool ShouldSerializeFastCombine() { return false; }
        [Category("Data Access Options"),DisplayName("Enable Legacy Redirects")]
        public bool LegacyRedirects {
            get { return MetadataObject.DataAccessOptions.LegacyRedirects; } 
            set { SetValue(LegacyRedirects, value, v => MetadataObject.DataAccessOptions.LegacyRedirects = (bool)v, Properties.LEGACYREDIRECTS); }
        }
        private bool ShouldSerializeLegacyRedirects() { return false; }
        [Category("Data Access Options"), DisplayName("Return Error Values As Nulll")]
        public bool ReturnErrorValuesAsNull
        {
            get { return MetadataObject.DataAccessOptions.ReturnErrorValuesAsNull; }
            set { SetValue(ReturnErrorValuesAsNull, value, v => MetadataObject.DataAccessOptions.ReturnErrorValuesAsNull = (bool)v, Properties.RETURNERRORVALUESASNULL); }
        }
        private bool ShouldSerializeReturnErrorValuesAsNull() { return false; }

        protected override void OnPropertyChanging(string propertyName, object newValue, ref bool undoable, ref bool cancel)
        {
            if(propertyName == Properties.DISCOURAGEIMPLICITMEASURES && DiscourageImplicitMeasures && (bool)newValue == false)
            {
                if (Model.CalculationGroups.Any()) throw new ArgumentOutOfRangeException(Properties.DISCOURAGEIMPLICITMEASURES, "This property must be set 'True' when a model contains calculation groups.");
            }
            base.OnPropertyChanging(propertyName, newValue, ref undoable, ref cancel);
        }

        public IEnumerable<CalculationGroupTable> CalculationGroups => Tables.OfType<CalculationGroupTable>();
    }

    internal static partial class Properties
    {
        public const string OBJECTTYPENAME = "ObjectTypeName";
        public const string FASTCOMBINE = "FastCombine";
        public const string LEGACYREDIRECTS = "LegacyRedirects";
        public const string RETURNERRORVALUESASNULL = "ReturnErrorValuesAsNull";
    }

}
