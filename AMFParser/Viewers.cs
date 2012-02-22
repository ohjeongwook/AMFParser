using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Viewers
{
    public partial class TXTEditor : UserControl
    {
        public string Data
        {
            get
            {
                return this.AMFEditorRichTextBox.Text;
            }
            set
            {
                this.AMFEditorRichTextBox.Text = value;
            }
        }

        public TXTEditor()
        {
            InitializeComponent();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
