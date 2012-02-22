namespace Viewers
{
    partial class TXTEditor
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.AMFEditorRichTextBox = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // AMFEditorRichTextBox
            // 
            this.AMFEditorRichTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.AMFEditorRichTextBox.Location = new System.Drawing.Point(0, 0);
            this.AMFEditorRichTextBox.Name = "AMFEditorRichTextBox";
            this.AMFEditorRichTextBox.Size = new System.Drawing.Size(403, 499);
            this.AMFEditorRichTextBox.TabIndex = 0;
            this.AMFEditorRichTextBox.Text = "";
            // 
            // AMFEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.AMFEditorRichTextBox);
            this.Name = "AMFEditor";
            this.Size = new System.Drawing.Size(403, 499);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.RichTextBox AMFEditorRichTextBox;

    }
}
