﻿extern alias json;

using System;
using System.Collections.Generic;
using System.Linq;
using TabularEditor.TOMWrapper.Undo;
using TOM = Microsoft.AnalysisServices.Tabular;
using System.Diagnostics;
using TabularEditor.TOMWrapper.PowerBI;
using TabularEditor.TOMWrapper.Utils;
using TabularEditor.Utils;

namespace TabularEditor.TOMWrapper
{
    /// <summary>
    /// 
    /// </summary>
    public sealed partial class TabularModelHandler : IDisposable
    {
        internal Guid InstanceID = Guid.NewGuid();
        public const string PROP_HASUNSAVEDCHANGES = "HasUnsavedChanges";
        public const string PROP_ISCONNECTED = "IsConnected";
        public const string PROP_STATUS = "Status";
        public const string PROP_ERRORS = "Errors";

        private TabularModelHandlerSettings _settings = TabularModelHandlerSettings.Default;
        public TabularModelHandlerSettings Settings
        {
            get
            { return _settings; }
            set
            {
                _settings = value;
                PowerBIGovernance.UpdateGovernanceMode(this);
                _tree?.OnStructureChanged();
            }
        }

        public UndoManager UndoManager { get; private set; }
        public TabularCommonActions Actions { get; private set; }

        private TOM.Server server = null;
        private TOM.Database database;

        public Model Model { get; private set; }
        public TOM.Database Database { get { return database; } }
        public string ConnectionInfo => IsConnected && database?.Server != null ? database.Server.Name : "(No connection)";
        public int CompatibilityLevel => database.CompatibilityLevel;
        public bool PbiMode => Database?.CompatibilityMode == Microsoft.AnalysisServices.CompatibilityMode.PowerBI;
        public bool IsPbiDesktop => PowerBIGovernance.IsPBIDesktop(database);
        /// <summary>
        /// Applys translation from a JSON string.
        /// </summary>
        /// <param name="culturesJson"></param>
        /// <param name="overwriteExisting"></param>
        /// <param name="ignoreInvalid"></param>
        /// <returns>False if ignoreInvalid is set to false and an invalid object is encountered</returns>
        public bool ImportTranslations(string culturesJson, bool overwriteExisting, bool ignoreInvalid)
        {
            BeginUpdate("Import translations");
            var result = TabularCultureHelper.ImportTranslations(culturesJson, Model, overwriteExisting, !ignoreInvalid);

            // Rolls back translation changes if an error were encountered
            EndUpdate(true, !result);

            return result;
        }

        private void Init()
        {
            UndoManager = new UndoManager(this);
            Actions = new TabularCommonActions(this);
            Model = Model.CreateFromMetadata(database.Model);
            Model.Database = new Database(Model, database);
            //CheckErrors();

            FormulaFixup.BuildDependencyTree();
        }

        internal readonly Dictionary<string, ITabularObjectCollection> WrapperCollections = new Dictionary<string, ITabularObjectCollection>();
        public TabularObject GetWrapperObject(TOM.MetadataObject obj) { return WrapperLookup[obj]; }
        internal readonly Dictionary<TOM.MetadataObject, TabularObject> WrapperLookup = new Dictionary<TOM.MetadataObject, TabularObject>();

        public ModelSourceType SourceType { get; private set; }
        public string Source { get; private set; }

        /// <summary>
        /// Creates a new blank Tabular Model
        /// </summary>
        public TabularModelHandler(int compatibilityLevel = 1200, TabularModelHandlerSettings settings = null, bool pbiDatasetModel = false)
        {
            Settings = settings ?? TabularModelHandlerSettings.Default;

            Singleton = this;
            server = null;

            database = new TOM.Database("SemanticModel") { CompatibilityLevel = compatibilityLevel,
                CompatibilityMode = pbiDatasetModel ? Microsoft.AnalysisServices.CompatibilityMode.PowerBI : Microsoft.AnalysisServices.CompatibilityMode.AnalysisServices };

            database.Model = new TOM.Model();
            if (pbiDatasetModel) database.Model.DefaultPowerBIDataSourceVersion = TOM.PowerBIDataSourceVersion.PowerBI_V3;

             SourceType = ModelSourceType.File;
            Source = "Model.bim";

            Status = "Succesfully created new model.";
            Init();

            UndoManager.Enabled = true;
            PowerBIGovernance.UpdateGovernanceMode(this);
        }
        internal PowerBIGovernance PowerBIGovernance { get; } = new PowerBIGovernance();

        private PowerBiTemplate pbit;

        private string serverName;

        private readonly string applicationName = "TabularEditor-" + Guid.NewGuid().ToString("D");

        /// <summary>
        /// Connects to a SQL Server 2016 Analysis Services instance and loads a tabular model
        /// from one of the deployed databases on the instance.
        /// </summary>
        /// <param name="serverName"></param>
        /// <param name="databaseName"></param>
        public TabularModelHandler(string serverName, string databaseName, TabularModelHandlerSettings settings = null)
        {
            this.serverName = serverName;
            _disableUpdates = true;

            Settings = settings ?? TabularModelHandlerSettings.Default;

            Singleton = this;
            server = new TOM.Server();

            var connectionString = TabularConnection.GetConnectionString(serverName, applicationName);
            server.Connect(connectionString);

            if (databaseName == null)
            {
                if (server.Databases.Count >= 1) database = server.Databases[0];
                else throw new InvalidOperationException("This instance does not contain any databases, or the user does not have access.");
            }
            else
            {
                database = server.Databases.GetByName(databaseName);
            }
            if (CompatibilityLevel < 1200) throw new InvalidOperationException("Only databases with Compatibility Level 1200 or higher can be loaded in Tabular Editor.");

            SourceType = ModelSourceType.Database;
            Source = database.Server.Name + "." + database.Name;

            Status = "Connected succesfully.";
            Version = database.Version;
            Init();

            Model.ClearTabularEditorAnnotations();

            _disableUpdates = false;
            UndoManager.Enabled = true;
            PowerBIGovernance.UpdateGovernanceMode(this);
            CheckErrors();
        }

        private static TabularModelHandler _singleton;
        internal static TabularModelHandler Singleton
        {
            get => _singleton;
            set
            {
                _singleton = value;
            }
        }

        public bool IsConnected { get { return Version != -1; } }
        public long Version { get; private set; } = -1;

        public bool HasUnsavedChanges
        {
            get
            {
                return !UndoManager.AtCheckpoint;
            }
        }

        private string _status;
        public string Status { get { return _status; } set { _status = value; } }

        internal HashSet<IErrorMessageObject> _errors { get; private set; } = new HashSet<IErrorMessageObject>();
        public IReadOnlyCollection<IErrorMessageObject> Errors => _errors;

        internal static List<Tuple<TOM.NamedMetadataObject, TOM.ObjectState>> GetObjectsNotReady(TOM.Database database)
        {
            var result = new List<Tuple<TOM.NamedMetadataObject, TOM.ObjectState>>();

            // Find partitions that are not in the "Ready" state:
            result.AddRange(
                    database.Model.Tables.SelectMany(t => t.Partitions).Where(p => p.State != TOM.ObjectState.Ready)
                    .Select(p => new Tuple<TOM.NamedMetadataObject, TOM.ObjectState>(p, p.State))
                    );

            // Find calculated columns that are not in the "Ready" state:
            result.AddRange(
                    database.Model.Tables.SelectMany(t => t.Columns.OfType<TOM.CalculatedColumn>()).Where(c => c.State != TOM.ObjectState.Ready)
                    .Select(c => new Tuple<TOM.NamedMetadataObject, TOM.ObjectState>(c, c.State))
                );

            return result;
        }

        internal static List<Tuple<TOM.NamedMetadataObject, string>> CheckErrors(TOM.Database database)
        {
            var result = new List<Tuple<TOM.NamedMetadataObject, string>>();
            foreach (var t in database.Model.Tables)
            {
                result.AddRange(t.Measures.Where(m => !string.IsNullOrEmpty(m.ErrorMessage)).Select(m => new Tuple<TOM.NamedMetadataObject, string>(m, m.ErrorMessage)));
                if (database.CompatibilityLevel >= 1400) result.AddRange(t.Measures.Where(m => !string.IsNullOrEmpty(m.DetailRowsDefinition?.ErrorMessage)).Select(m => new Tuple<TOM.NamedMetadataObject, string>(m, "Detail rows expression: " + m.DetailRowsDefinition.ErrorMessage)));
                result.AddRange(t.Columns.Where(c => !string.IsNullOrEmpty(c.ErrorMessage)).Select(c => new Tuple<TOM.NamedMetadataObject, string>(c, c.ErrorMessage)));
                result.AddRange(t.Partitions.Where(p => !string.IsNullOrEmpty(p.ErrorMessage)).Select(p => new Tuple<TOM.NamedMetadataObject, string>(p, p.ErrorMessage)));
                if(database.CompatibilityLevel >= 1470 && t.CalculationGroup != null)
                {
                    result.AddRange(t.CalculationGroup.CalculationItems.Where(ci => !string.IsNullOrEmpty(ci.ErrorMessage)).Select(ci => new Tuple<TOM.NamedMetadataObject, string>(ci, ci.ErrorMessage)));
                    result.AddRange(t.CalculationGroup.CalculationItems.Where(ci => !string.IsNullOrEmpty(ci.FormatStringDefinition?.ErrorMessage)).Select(ci => new Tuple<TOM.NamedMetadataObject, string>(ci, "Format string expression: " + ci.FormatStringDefinition.ErrorMessage)));
                }
            }
            foreach(var r in database.Model.Roles)
            {
                result.AddRange(r.TablePermissions.Where(tp => !string.IsNullOrEmpty(tp.ErrorMessage)).Select(tp => new Tuple<TOM.NamedMetadataObject, string>(tp, tp.ErrorMessage)));
            }
            return result;
        }

        private void CheckErrors()
        {
            var errorList = new List<IErrorMessageObject>();

            Tree.ClearFolderErrors();

            foreach (var t in database.Model.Tables)
            {
                var table = GetWrapperObject(t) as Table;
                table.ClearError();
                errorList.AddRange(table.Measures.Where(m => !string.IsNullOrEmpty(m.ErrorMessage)));
                errorList.AddRange(table.Columns.Where(c => !string.IsNullOrEmpty(c.ErrorMessage)));
                errorList.AddRange(table.Partitions.Where(p => !string.IsNullOrEmpty(p.ErrorMessage)));
                if (CompatibilityLevel >= 1470 && table is CalculationGroupTable cgt)
                    errorList.AddRange(cgt.CalculationItems.Where(ci => !string.IsNullOrEmpty(ci.ErrorMessage)));

                WrapperLookup.Values.OfType<IExpressionObject>().ToList().ForEach(i => i.ResetModifiedState());
            }
            errorList.AddRange(Model.Roles.Where(r => r.ErrorMessage != null));
            if (errorList.Count > 0 || Errors?.Count > 0)
            {
                _errors = new HashSet<IErrorMessageObject>(errorList);
            }

            foreach(var errObj in errorList)
            {
                if (errObj is IFolderObject fo)
                {
                    var parentFolder = fo.GetFolder(Tree.Culture);
                    if (parentFolder != null) parentFolder.AddError(fo);
                    else if(fo is ITabularTableObject tto) tto.Table.AddError(fo);
                }
                else if (errObj is CalculationItem ci)
                {
                    var parentTable = ci.CalculationGroupTable;
                    parentTable.AddError(ci);
                }
            }
        }

        private bool _disableUpdates = false;
        

        /// <summary>
        /// Begins a batch update
        /// </summary>
        /// <param name="undoName"></param>
        public void BeginUpdate(string undoName)
        {
            if (_disableUpdates) return;

            EoB_PostponeOperations = true;

            Tree.BeginUpdate();
            if(!string.IsNullOrEmpty(undoName)) UndoManager.BeginBatch(undoName);
        }

        /// <summary>
        /// Ends the latest batch update (can never be called more times than BeginUpdate).
        /// </summary>
        /// <param name="undoable"></param>
        /// <param name="rollback"></param>
        /// <returns></returns>
        public int EndUpdate(bool undoable = true, bool rollback = false)
        {
            if (_disableUpdates) return 0;

            var actionCount = 0;
            if(undoable || rollback) actionCount = UndoManager.EndBatch(rollback);

            if (Tree.UpdateLocks == 1) EoB_PostponeOperations = false;
            if (Tree.UpdateLocks == 1 && EoB_RequireRebuildDependencyTree)
            {
                FormulaFixup.BuildDependencyTree();
                EoB_RequireRebuildDependencyTree = false;
            }

            // This takes care of reducing the UpdateLocks counter, and notifying the UI when we reach zero:
            Tree.EndUpdate();

            return actionCount;
        }

        internal bool EoB_RequireRebuildDependencyTree = false;
        internal bool EoB_PostponeOperations = false;
        public bool UpdateInProgress => EoB_PostponeOperations;

        /// <summary>
        /// Ends all batch updates in progress.
        /// </summary>
        /// <param name="rollback"></param>
        /// <returns></returns>
        public int EndUpdateAll(bool rollback = false)
        {
            var actionCount = 0;
            while (UndoManager.BatchDepth > 0)
            {
                actionCount = UndoManager.EndBatch(rollback);
            }

            if (EoB_RequireRebuildDependencyTree) { EoB_PostponeOperations = false; FormulaFixup.BuildDependencyTree(); EoB_RequireRebuildDependencyTree = false; }

            while (Tree.UpdateLocks > 0)
            {
                Tree.EndUpdate();
            }
            return actionCount;
        }


        internal void UpdateObject(ITabularObject obj)
        {

            Tree.OnNodesChanged(obj);
        }
        internal void UpdateObjectName(ITabularNamedObject obj)
        {
            Tree.OnNodeNameChanged(obj);
        }
 
        internal void UpdateFolders(Table table)
        {
            Tree.RebuildFolderCacheForTable(table);
            Tree.OnStructureChanged(table);
        }
        internal void UpdateLevels(Hierarchy hierarchy)
        {
            Tree.OnStructureChanged(hierarchy);
        }

        public void Dispose()
        {
            if(server != null)
            {
                server.Dispose();
            }
        }

        private TabularTree _tree;
        public TabularTree Tree {
            get
            {
                if(_tree == null)
                {
                    _tree = new NullTree(this);
                }
                return _tree;
            }
            set
            {
                _tree = value;
            }
        }
    }
}