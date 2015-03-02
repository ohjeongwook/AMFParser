using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Viewers
{
    public partial class ElementEdit : Form
    {
        public ElementEdit()
        {
            InitializeComponent();

            updateButton.DialogResult = DialogResult.OK;
            cancelButton.DialogResult = DialogResult.Cancel;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) this.Close();
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public String EditText
        {
            get { return editTextBox.Text; }
            set { editTextBox.Text = value; }
        }
    }
}
