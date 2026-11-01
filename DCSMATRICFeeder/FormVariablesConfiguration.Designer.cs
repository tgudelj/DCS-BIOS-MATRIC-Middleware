
namespace EXM.DBMM {
    partial class FormVariablesConfiguration {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormVariablesConfiguration));
            ddAircraft = new ComboBox();
            lblAircraft = new Label();
            label1 = new Label();
            btnSave = new Button();
            btnCancel = new Button();
            lblAvailable = new Label();
            lstAvailableVariables = new ListBox();
            lblCategory = new Label();
            ddCategory = new ComboBox();
            lstConfiguredVariables = new ListBox();
            btnAddSelected = new Button();
            btnRemoveSelected = new Button();
            btnAddAll = new Button();
            btnRemoveAll = new Button();
            txtAvailableFilter = new TextBox();
            btnClearAvailableFilter = new Button();
            btnClearConfiguredFilter = new Button();
            txtConfiguredFilter = new TextBox();
            lblSelectionHint = new Label();
            SuspendLayout();
            // 
            // ddAircraft
            // 
            ddAircraft.DropDownStyle = ComboBoxStyle.DropDownList;
            ddAircraft.Font = new Font("Segoe UI", 10F);
            ddAircraft.FormattingEnabled = true;
            ddAircraft.Location = new Point(198, 8);
            ddAircraft.Name = "ddAircraft";
            ddAircraft.Size = new Size(241, 25);
            ddAircraft.TabIndex = 0;
            ddAircraft.SelectedIndexChanged += ddAircraft_SelectedIndexChanged;
            // 
            // lblAircraft
            // 
            lblAircraft.Font = new Font("Segoe UI", 10F);
            lblAircraft.Location = new Point(12, 9);
            lblAircraft.Name = "lblAircraft";
            lblAircraft.Size = new Size(180, 22);
            lblAircraft.TabIndex = 14;
            lblAircraft.Text = "Aircraft name";
            lblAircraft.Click += lblAircraft_Click;
            // 
            // label1
            // 
            label1.Font = new Font("Segoe UI", 10F);
            label1.Location = new Point(447, 90);
            label1.Name = "label1";
            label1.Size = new Size(343, 22);
            label1.TabIndex = 16;
            label1.Text = "Variables to export from DCS-BIOS to MATRIC";
            // 
            // btnSave
            // 
            btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.Font = new Font("Segoe UI", 10F);
            btnSave.Location = new Point(696, 551);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(96, 37);
            btnSave.TabIndex = 10;
            btnSave.Text = "Save";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Font = new Font("Segoe UI", 10F);
            btnCancel.Location = new Point(594, 551);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(96, 37);
            btnCancel.TabIndex = 9;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // lblAvailable
            // 
            lblAvailable.Font = new Font("Segoe UI", 10F);
            lblAvailable.Location = new Point(12, 90);
            lblAvailable.Name = "lblAvailable";
            lblAvailable.Size = new Size(345, 22);
            lblAvailable.TabIndex = 20;
            lblAvailable.Text = "Available variables";
            // 
            // lstAvailableVariables
            // 
            lstAvailableVariables.FormattingEnabled = true;
            lstAvailableVariables.ItemHeight = 15;
            lstAvailableVariables.Location = new Point(12, 147);
            lstAvailableVariables.Name = "lstAvailableVariables";
            lstAvailableVariables.SelectionMode = SelectionMode.MultiExtended;
            lstAvailableVariables.Size = new Size(343, 394);
            lstAvailableVariables.TabIndex = 3;
            // 
            // lblCategory
            // 
            lblCategory.Font = new Font("Segoe UI", 10F);
            lblCategory.Location = new Point(12, 49);
            lblCategory.Name = "lblCategory";
            lblCategory.Size = new Size(180, 22);
            lblCategory.TabIndex = 23;
            lblCategory.Text = "Category";
            lblCategory.Click += lblCategory_Click;
            // 
            // ddCategory
            // 
            ddCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            ddCategory.Font = new Font("Segoe UI", 10F);
            ddCategory.FormattingEnabled = true;
            ddCategory.Location = new Point(198, 49);
            ddCategory.Name = "ddCategory";
            ddCategory.Size = new Size(241, 25);
            ddCategory.TabIndex = 1;
            ddCategory.SelectedIndexChanged += ddCategory_SelectedIndexChanged;
            // 
            // lstConfiguredVariables
            // 
            lstConfiguredVariables.FormattingEnabled = true;
            lstConfiguredVariables.ItemHeight = 15;
            lstConfiguredVariables.Location = new Point(447, 147);
            lstConfiguredVariables.Name = "lstConfiguredVariables";
            lstConfiguredVariables.SelectionMode = SelectionMode.MultiExtended;
            lstConfiguredVariables.Size = new Size(343, 394);
            lstConfiguredVariables.TabIndex = 8;
            // 
            // btnAddSelected
            // 
            btnAddSelected.Font = new Font("Segoe UI", 12F);
            btnAddSelected.Location = new Point(373, 202);
            btnAddSelected.Name = "btnAddSelected";
            btnAddSelected.Size = new Size(46, 46);
            btnAddSelected.TabIndex = 4;
            btnAddSelected.Text = ">";
            btnAddSelected.UseVisualStyleBackColor = true;
            btnAddSelected.Click += btnAddSelected_Click;
            // 
            // btnRemoveSelected
            // 
            btnRemoveSelected.Font = new Font("Segoe UI", 12F);
            btnRemoveSelected.Location = new Point(373, 435);
            btnRemoveSelected.Name = "btnRemoveSelected";
            btnRemoveSelected.Size = new Size(46, 46);
            btnRemoveSelected.TabIndex = 7;
            btnRemoveSelected.Text = "<";
            btnRemoveSelected.UseVisualStyleBackColor = true;
            btnRemoveSelected.Click += btnRemoveSelected_Click;
            // 
            // btnAddAll
            // 
            btnAddAll.Font = new Font("Segoe UI", 12F);
            btnAddAll.Location = new Point(373, 258);
            btnAddAll.Name = "btnAddAll";
            btnAddAll.Size = new Size(46, 46);
            btnAddAll.TabIndex = 5;
            btnAddAll.Text = ">>";
            btnAddAll.UseVisualStyleBackColor = true;
            btnAddAll.Click += btnAddAll_Click;
            // 
            // btnRemoveAll
            // 
            btnRemoveAll.Font = new Font("Segoe UI", 12F);
            btnRemoveAll.Location = new Point(373, 383);
            btnRemoveAll.Name = "btnRemoveAll";
            btnRemoveAll.Size = new Size(46, 46);
            btnRemoveAll.TabIndex = 6;
            btnRemoveAll.Text = "<<";
            btnRemoveAll.UseVisualStyleBackColor = true;
            btnRemoveAll.Click += btnRemoveAll_Click;
            // 
            // txtAvailableFilter
            // 
            txtAvailableFilter.Font = new Font("Segoe UI", 11F);
            txtAvailableFilter.Location = new Point(12, 115);
            txtAvailableFilter.Name = "txtAvailableFilter";
            txtAvailableFilter.Size = new Size(310, 27);
            txtAvailableFilter.TabIndex = 1;
            txtAvailableFilter.TextChanged += SearchFilters_TextChanged;
            // 
            // btnClearAvailableFilter
            // 
            btnClearAvailableFilter.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnClearAvailableFilter.Location = new Point(328, 115);
            btnClearAvailableFilter.Name = "btnClearAvailableFilter";
            btnClearAvailableFilter.Size = new Size(27, 27);
            btnClearAvailableFilter.TabIndex = 30;
            btnClearAvailableFilter.Text = "X";
            btnClearAvailableFilter.UseVisualStyleBackColor = true;
            btnClearAvailableFilter.Click += btnClearAvailableFilter_Click;
            // 
            // btnClearConfiguredFilter
            // 
            btnClearConfiguredFilter.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnClearConfiguredFilter.Location = new Point(763, 115);
            btnClearConfiguredFilter.Name = "btnClearConfiguredFilter";
            btnClearConfiguredFilter.Size = new Size(27, 27);
            btnClearConfiguredFilter.TabIndex = 32;
            btnClearConfiguredFilter.Text = "X";
            btnClearConfiguredFilter.UseVisualStyleBackColor = true;
            btnClearConfiguredFilter.Click += btnClearConfiguredFilter_Click;
            // 
            // txtConfiguredFilter
            // 
            txtConfiguredFilter.Font = new Font("Segoe UI", 11F);
            txtConfiguredFilter.Location = new Point(447, 115);
            txtConfiguredFilter.Name = "txtConfiguredFilter";
            txtConfiguredFilter.Size = new Size(310, 27);
            txtConfiguredFilter.TabIndex = 31;
            txtConfiguredFilter.TextChanged += SearchFilters_TextChanged;
            //
            // lblSelectionHint
            //
            lblSelectionHint.AutoEllipsis = true;
            lblSelectionHint.Dock = DockStyle.Bottom;
            lblSelectionHint.Location = new Point(0, 600);
            lblSelectionHint.Name = "lblSelectionHint";
            lblSelectionHint.Size = new Size(804, 24);
            lblSelectionHint.TabIndex = 33;
            lblSelectionHint.Text = "No saved selection: export all. Saving an empty selection: export none for that module.";
            // 
            // FormVariablesConfiguration
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(804, 624);
            Controls.Add(lblSelectionHint);
            Controls.Add(btnClearConfiguredFilter);
            Controls.Add(txtConfiguredFilter);
            Controls.Add(btnClearAvailableFilter);
            Controls.Add(txtAvailableFilter);
            Controls.Add(btnRemoveAll);
            Controls.Add(btnAddAll);
            Controls.Add(btnRemoveSelected);
            Controls.Add(btnAddSelected);
            Controls.Add(lstConfiguredVariables);
            Controls.Add(lblCategory);
            Controls.Add(ddCategory);
            Controls.Add(lstAvailableVariables);
            Controls.Add(lblAvailable);
            Controls.Add(btnCancel);
            Controls.Add(btnSave);
            Controls.Add(label1);
            Controls.Add(lblAircraft);
            Controls.Add(ddAircraft);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new Size(816, 545);
            Name = "FormVariablesConfiguration";
            Text = "Export variables configuration";
            Load += FormVariablesFilter_Load;
            ResumeLayout(false);
            PerformLayout();
        }



        #endregion

        private ComboBox ddAircraft;
        private Label lblAircraft;
        private Label label1;
        private Button btnSave;
        private Button btnCancel;
        private Label lblAvailable;
        private ListBox lstAvailableVariables;
        private Label lblCategory;
        private ComboBox ddCategory;
        private ListBox lstConfiguredVariables;
        private Button btnAddSelected;
        private Button btnRemoveSelected;
        private Button btnAddAll;
        private Button btnRemoveAll;
        private TextBox txtAvailableFilter;
        private Button btnClearAvailableFilter;
        private Button btnClearConfiguredFilter;
        private TextBox txtConfiguredFilter;
        private Label lblSelectionHint;
    }
}
