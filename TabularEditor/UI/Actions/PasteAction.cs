﻿using Crad.Windows.Forms.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TabularEditor.TOMWrapper;
using TabularEditor.TOMWrapper.Utils;

namespace TabularEditor.UI.Actions
{
    public class PasteAction: ClipboardAction
    {
        public PasteAction()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.ShortcutKeys = (Keys.Control | Keys.V);
            this.Text = "&Paste";
            this.ToolTipText = "Paste";
        }

        protected override void OnExecute(EventArgs e)
        {
            if (ActiveTextBox != null)
            {
                // PropertyGrids handle copy/paste on their own - below check fixes issue #20
                if (!PropertyGridHelper.IsPropertyGridControl(ActiveTextBox.Parent))
                    ActiveTextBox.Paste();
            }
            else if (SelectedNodes != null && UIController.Current.ClipboardObjects != null)
            {
                var pasteItems = UIController.Current.ClipboardObjects;
                var inserted = UIController.Current.Handler.Actions.InsertObjects(pasteItems, SelectedNodes[0].Tag as ITabularNamedObject);
                foreach (var item in inserted.OfType<ITabularPerspectiveObject>()) item.Vis();
                if (inserted.Count > 0)
                {
                    UIController.Current.Goto(inserted[0] as ITabularNamedObject);
                    UIController.Current.Elements.TreeView.BeginUpdate();
                    foreach (var item in inserted)
                    {
                        var node = UIController.Current.Elements.TreeView.FindNodeByTag(item);
                        if (node != null) node.IsSelected = true;
                    }
                    UIController.Current.Elements.TreeView.EndUpdate();
                }
            }
            base.OnExecute(e);
        }

        protected override void OnUpdate(EventArgs e)
        {
            try
            {
                Enabled = (ActiveTextBox != null && Clipboard.ContainsData(DataFormats.Text) && UIController.Current.ClipboardObjects == null) ||
                    (UIController.Current.ClipboardObjects?.Count > 0 && TreeHasFocus);
            }
            catch {
                Enabled = false;
            }

            base.OnUpdate(e);
        }
    }
}
