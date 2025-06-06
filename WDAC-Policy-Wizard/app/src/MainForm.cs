﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// jogeurte 11/19

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using WDAC_Wizard.src;
using WDAC_Wizard.Properties;
namespace WDAC_Wizard
{
    public partial class MainWindow : Form
    {
        // MainWindow Specific Private Properties 
        private bool ConfigInProcess;                    // Has a user selected one of the following views: New, Audit, Edit, Merge
        private int CurrentPage;                         // Which user control is sitting on top of MainWindow 
        public byte view { get; set; }                   // 1 - New policy ,, 2 - Edit policy,, 3 - Merge policy
        public bool ErrorOnPage { get; set; }            // Flag blocking proceeding to next user control. 
        public string ErrorMsg { get; set; }             // Accompanying message error description for ErrorOnPage flag 
        public bool RedoFlowRequired { get; set; }       // Flag which prohibts user from making changes on page 1 then skipping back to page 4, for instance
        public bool CustomRuleinProgress { get; set; }   // Flag set when user has kicked off the custom rule procedure. Ensures user does not go to build page without accidentally creating the rule

        public List<string> PageList;
        public WDAC_Policy Policy { get; set; }
        public List<CiEvent> CiEvents { get; set; }

        private int RulesNumber;
        private int nCustomValueRules;

        public string TempFolderPath { get; set; }
        public string ExeFolderPath { get; set; }

        // Edit Workflow datastructs
        private BuildPage _BuildPage;
        private SigningRules_Control _SigningRulesControl;
        public EditWorkflowType EditWorkflow;
        public SiPolicy EventLogPolicy; 

        public enum EditWorkflowType
        {
            Edit = 0,
            EventLog = 1,
        }

        public MainWindow()
        {
            TempFolderPath = CreateTempFolder();
            Logger.NewLogger(TempFolderPath); 
            
            InitializeComponent();

            // Init MainWindow params
            ConfigInProcess = false;
            CurrentPage = 0;
            RulesNumber = 0;
            nCustomValueRules = 0;

            Policy = new WDAC_Policy();
            CiEvents = new List<CiEvent>(); 
            PageList = new List<string>();
            ExeFolderPath = Helper.GetExecutablePath(false);

            CustomRuleinProgress = false; 

            // Check for configci cmdlet availability
            Helper.LicenseCheck(); 
        }

        // ###############
        // HEADER CONTROLS
        // ###############

        /// <summary>
        /// New policy button selected: User can select either base or suppl policy,
        /// a template policy then customize policy rules and file rules.
        /// </summary>
        /// 
        private void Button_New_Click(object sender, EventArgs e)
        {
            if (!ConfigInProcess)
            {
                Logger.Log.AddNewSeparationLine("Workflow -- New Policy Selected");
                view = 1; 
                CurrentPage = 1;
                ConfigInProcess = true;
                RedoFlowRequired = false;
                Policy.PolicyWorkflow = WDAC_Policy.Workflow.New;
                Policy._PolicyType = WDAC_Policy.PolicyType.BasePolicy;

                PageController(sender, e); 
                button_Next.Visible = true;
            }

            else
            {
                // Working on other workflow - do you want to leave?
                if (WantToAbandonWork())
                {
                    DisplayInfoText(0);
                    ConfigInProcess = false;
                    Button_New_Click(sender, e);
                }
            }
        }

        
        /// <summary>
        /// Edit policy button selected: User can load a pre-exisiting policy on disk and 
        /// view and reconfigure its rules and settings. 
        /// </summary>
        private void Button_Edit_Click(object sender, EventArgs e)
        {
            // Edit Policy Button:
           
            if (!ConfigInProcess)
            {
                Logger.Log.AddNewSeparationLine("Workflow -- Edit Policy Selected");
                view = 2;
                CurrentPage = 1; 
                ConfigInProcess = true;
                Policy.PolicyWorkflow = WDAC_Policy.Workflow.Edit;

                PageController(sender, e);
                button_Next.Visible = true;
            }

            else
            {
                // Working on other workflow - do you want to leave?
                // If so, set the ConfigInProcess flag to false
                if (WantToAbandonWork())
                {
                    DisplayInfoText(0);
                    ConfigInProcess = false;
                    Button_Edit_Click(sender, e);
                }
            }
        }


        /// <summary>
        /// Merge policy button selected: User must select two policies on disk to merge into 
        /// one single policy where the new policy is the intersection of the former two. 
        /// </summary>
        private void Button_Merge_Click(object sender, EventArgs e)
        {
            if (!ConfigInProcess)
            {
                Logger.Log.AddNewSeparationLine("Workflow -- Merge Policies Selected");
                view = 3;
                CurrentPage = 1;
                ConfigInProcess = true;
                Policy.PolicyWorkflow = WDAC_Policy.Workflow.Merge;

                PageController(sender, e);
                button_Next.Visible = true;
            }

            else
            {
                // Working on other workflow - do you want to leave?
                // If so, set the ConfigInProcess flag to false
                if (WantToAbandonWork())
                {
                    DisplayInfoText(0);
                    ConfigInProcess = false;
                    Button_Merge_Click(sender, e);
                }
            }
        }

        // #####################
        // CONTROL PANEL CONTROLS
        // #####################

        /// <summary>
        /// Home button on the left hand navigation panel. Resets the state of the application and brings user to the MainWindow form.  
        /// </summary>
        private void Home_Button_Click(object sender, EventArgs e)
        {
            button_Next.Visible = false;

            // If the CustomRules Panel is open, close it
            if (CustomRuleinProgress && _SigningRulesControl != null)
            {
                DialogResult res = MessageBox.Show("Do you want to return home and abandon this custom rule?",
                        "Confirmation",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                if (res == DialogResult.Yes)
                {
                    // Close the CustomRuleConditionsPanel
                    _SigningRulesControl.CloseCustomRulesPanel(); 
                }
                else
                {
                    // Do not return home
                    return; 
                }
            }

            // Re-initialize SiPolicy object
            Policy = new WDAC_Policy();

            // Reset flags
            ConfigInProcess = false;
            CustomRuleinProgress = false; 
            view = 0;
            CurrentPage = 0;
            Logger.NewLogger(TempFolderPath);

            RemoveControls(); 
            ShowControlPanel(sender, e);
        }
        /// <summary>
        /// Settings button on the left hand navigation panel. Loads the Settings UserControl. 
        /// </summary>
        private void Settings_Button_Click(object sender, EventArgs e)
        {
            Logger.Log.AddInfoMsg("Workflow -- Settings Button Selected");
            button_Next.Visible = false;
            CurrentPage = 99; 

            var _SettingsPage = new SettingsPage(this);
            _SettingsPage.Name = "SettingsPage"; 
            Controls.Add(_SettingsPage);
            PageList.Add(_SettingsPage.Name); 
            _SettingsPage.BringToFront();
            _SettingsPage.Focus();

            ShowControlPanel(sender, e);
        }

        /// <summary>
        /// Controls the PageController method when the user presses the Next button.
        /// If there is an ErrorOnPage flag, the page is not advanced. 
        /// </summary>
        private void Button_Next_Click(object sender, EventArgs e)
        {
            if (!ErrorOnPage)
            {
                // Check that a custom rule is not in progress
                if(CustomRuleinProgress)
                {
                    DialogResult res = MessageBox.Show("Do you want to create this custom rule before building your policy?", 
                        "Confirmation", 
                        MessageBoxButtons.YesNo, 
                        MessageBoxIcon.Question);

                    if (res == DialogResult.Yes)
                        return;
                }

                CurrentPage++;
                PageController(sender, e);
            }
                
            else
                DisplayInfoText(99); 
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PageNButton_Click(object sender, EventArgs e)
        {
            // Get the button name, extract the number of page user wants, set to CurrPage
            string buttonName = ((Button)sender).Name;
            int desiredPage = Convert.ToInt32(buttonName.Substring(4,1)); //eg. Page4_Button --> 4

            if (CurrentPage == desiredPage)
                return; 
            CurrentPage = desiredPage;

            // If settings page was previous, re-enable Next button
            button_Next.Visible = true; 
            PageController(sender, e);
        }

        /// <summary>
        /// Controller mechanism to determine which UserControls to place ontop of the MainWindow WinForm.
        /// Method called by the Next and Back button.
        /// </summary>
        public void PageController(object sender, EventArgs e)
        {
            DisplayInfoText(0);
            
            //RemoveControls(); 

            // Get pertitent workflow
            switch (CurrentPage){
            // Home page
            case 0:
                break;

            // 1st page
            case 1:
                    // New Workflow
                    if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.New)
                    {
                        // New Base Policy Workflow
                        if(Policy._PolicyType == WDAC_Policy.PolicyType.BasePolicy)
                        {
                            // New view - load choose base vs supplemental
                            string pageKey = "PolicyTypePage";
                            if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, launch instance
                            {
                                Control[] _Pages = Controls.Find(pageKey, true);
                                _Pages[0].Show();
                                _Pages[0].BringToFront();
                                _Pages[0].Focus();
                            }
                            else
                            {
                                var _PolicyTypePage = new PolicyType(this);
                                _PolicyTypePage.Name = pageKey;
                                PageList.Add(_PolicyTypePage.Name);
                                Controls.Add(_PolicyTypePage);
                                _PolicyTypePage.BringToFront();
                                _PolicyTypePage.Focus();
                            }

                            ShowControlPanel(sender, e);
                            break;
                        }

                        else if(Policy._PolicyType == WDAC_Policy.PolicyType.SupplementalPolicy)
                        {
                            // New view - load choose base vs supplemental
                            string pageKey = "PolicyTypePage";
                            if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, launch instance
                            {
                                Control[] _Pages = Controls.Find(pageKey, true);
                                _Pages[0].Show();
                                _Pages[0].BringToFront();
                                _Pages[0].Focus();
                            }
                            else
                            {
                                var _PolicyTypePage = new PolicyType(this);
                                _PolicyTypePage.Name = pageKey;
                                PageList.Add(_PolicyTypePage.Name);
                                Controls.Add(_PolicyTypePage);
                                _PolicyTypePage.BringToFront();
                                _PolicyTypePage.Focus();
                            }

                            ShowControlPanel(sender, e);
                        }
                    }

                    // Edit Workflow
                    else if(Policy.PolicyWorkflow == WDAC_Policy.Workflow.Edit)
                    {
                        // view & edit mode
                        // Show policy rules UI
                        string pageKey  = "EditWorkflowPage";
                        if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, launch instance
                        {
                            Control[] _Pages = Controls.Find(pageKey, true);
                            _Pages[0].Show();
                            _Pages[0].BringToFront();
                            _Pages[0].Focus();
                        }
                        else
                        {
                            var _EditWorkflow = new EditWorkflow(this);
                            _EditWorkflow.Name = pageKey;
                            PageList.Add(_EditWorkflow.Name);
                            Controls.Add(_EditWorkflow);
                            _EditWorkflow.BringToFront();
                            _EditWorkflow.Focus();
                        }

                        ShowControlPanel(sender, e);
                    }

                    // Merge Workflow
                    else if(Policy.PolicyWorkflow == WDAC_Policy.Workflow.Merge)
                    {
                        string pageKey  = "MergePage";
                        if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, launch instance
                        {
                            Control[] _Pages = Controls.Find(pageKey, true);
                            _Pages[0].Show();
                            _Pages[0].BringToFront();
                            _Pages[0].Focus();
                        }
                        else
                        {
                            var _MergePage = new PolicyMerge_Control(this);
                            _MergePage.Name = pageKey;
                            PageList.Add(_MergePage.Name);
                            Controls.Add(_MergePage);
                            _MergePage.BringToFront();
                            _MergePage.Focus();
                        }

                        ShowControlPanel(sender, e);
                    }

            break; // end of 1st page

            case 2: // 2nd Page

                    // New Workflow
                    if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.New)
                    {
                        // New Base Policy Workflow
                        if (Policy._PolicyType == WDAC_Policy.PolicyType.BasePolicy)
                        {
                            // New BASE policy - template page
                            string pageKey = "TemplatePage";
                            if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, launch instance
                            {
                                Control[] _Pages = Controls.Find(pageKey, true);
                                _Pages[0].Show();
                                _Pages[0].BringToFront();
                                _Pages[0].Focus();
                            }
                            else
                            {
                                var _NewPolicyPage = new TemplatePage(this);
                                _NewPolicyPage.Name = pageKey;
                                PageList.Add(_NewPolicyPage.Name);
                                Controls.Add(_NewPolicyPage);
                                _NewPolicyPage.BringToFront();
                                _NewPolicyPage.Focus();
                            }

                            ShowControlPanel(sender, e);
                        }

                        else //if (this.Policy._PolicyType == WDAC_Policy.PolicyType.SupplementalPolicy)
                        {
                            // New SUPPLEMENTAL policy -> policy rules. Do not present a template page
                            string pageKey  = "SupConfigTemplatePage";
                            if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, show instance
                            {
                                Control[] _Pages = Controls.Find(pageKey, true);
                                _Pages[0].Show();
                                _Pages[0].BringToFront();
                                _Pages[0].Focus();
                            }
                            else
                            {
                                var _ConfigTemplateControl = new ConfigTemplate_Control(this);
                                _ConfigTemplateControl.Name = pageKey;
                                PageList.Add(_ConfigTemplateControl.Name);
                                Controls.Add(_ConfigTemplateControl);
                                _ConfigTemplateControl.BringToFront();
                                _ConfigTemplateControl.Focus();
                            }
                            ShowControlPanel(sender, e);
                        }
                    }
                    // Edit Workflow
                    else if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.Edit)
                    {
                        // Edit Mode
                        string pageKey = "EditPolicyRulesPage";
                        if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, launch instance
                        {
                            Control[] _Pages = Controls.Find(pageKey, true);
                            _Pages[0].Show();
                            _Pages[0].BringToFront();
                            _Pages[0].Focus();
                        }
                        else
                        {
                            // CHECKS HERE IF EDIT FLOW OR AUDIT FLOW
                            if (EditWorkflow == EditWorkflowType.Edit)
                            {
                                var _RulesPage = new ConfigTemplate_Control(this);
                                _RulesPage.Name = pageKey;
                                PageList.Add(_RulesPage.Name);
                                Controls.Add(_RulesPage);
                                _RulesPage.BringToFront();
                                _RulesPage.Focus();
                            }
                            else
                            {
                                var _RulesPage = new EventLogRuleConfiguration(this);
                                _RulesPage.Name = pageKey;
                                PageList.Add(_RulesPage.Name);
                                Controls.Add(_RulesPage);
                                _RulesPage.BringToFront();
                                _RulesPage.Focus();
                            }
                        }

                        ShowControlPanel(sender, e);
                    }

                    // Merge Workflow
                    else if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.Merge)
                    {
                        button_Next.Visible = false;

                        string pageKey  = "BuildPage";
                        if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, show instance
                        {
                            Control[] _Pages = Controls.Find(pageKey, true);
                            _Pages[0].Show();
                            _Pages[0].BringToFront();
                            _Pages[0].Focus();
                        }
                        else
                        {
                            _BuildPage = new BuildPage(this);
                            _BuildPage.Name = pageKey;
                            PageList.Add(_BuildPage.Name);
                            Controls.Add(_BuildPage);
                            _BuildPage.BringToFront();
                            _BuildPage.Focus();
                        }
                        ShowControlPanel(sender, e);
                        ProcessPolicy();
                    }

            break; // end of 2nd page
                        

            // 3rd Page
            case 3:
                    // New policy workflow
                    if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.New)
                    {
                        // New Base
                        if(Policy._PolicyType == WDAC_Policy.PolicyType.BasePolicy)
                        {
                            string pageKey = "ConfigTemplatePage";
                            if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, show instance
                            {
                                Control[] _Pages = Controls.Find(pageKey, true);
                                _Pages[0].Show();
                                _Pages[0].BringToFront();
                                _Pages[0].Focus();
                            }
                            else
                            {
                                var _ConfigTemplateControl = new ConfigTemplate_Control(this);
                                _ConfigTemplateControl.Name = pageKey;
                                PageList.Add(_ConfigTemplateControl.Name);
                                Controls.Add(_ConfigTemplateControl);
                                _ConfigTemplateControl.BringToFront();
                                _ConfigTemplateControl.Focus();
                            }

                            ShowControlPanel(sender, e);
                        }

                        // New Supplemental
                        if(Policy._PolicyType == WDAC_Policy.PolicyType.SupplementalPolicy
                            || Policy._PolicyType == WDAC_Policy.PolicyType.AppIdTaggingPolicy)
                        {
                            string pageKey = "SupSigningRulesPage";
                            if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, show instance
                            {
                                Control[] _Pages = Controls.Find(pageKey, true);
                                _Pages[0].Show();
                                _Pages[0].BringToFront();
                                _Pages[0].Focus();
                            }
                            else
                            {
                                _SigningRulesControl = new SigningRules_Control(this);
                                _SigningRulesControl.Name = pageKey;
                                PageList.Add(_SigningRulesControl.Name);
                                Controls.Add(_SigningRulesControl);
                                _SigningRulesControl.BringToFront();
                                _SigningRulesControl.Focus();
                            }

                            ShowControlPanel(sender, e);
                        }
                    }

                    // Edit policy workflow
                    else if(Policy.PolicyWorkflow == WDAC_Policy.Workflow.Edit)
                    {
                        // Edit & view mode
                        string pageKey = "SigningRulesPage";
                        if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, show instance
                        {
                            Control[] _Pages = Controls.Find(pageKey, true);
                            _Pages[0].Show();
                            _Pages[0].BringToFront();
                            _Pages[0].Focus();
                        }
                        else
                        {
                            // CHECKS HERE IF EDIT FLOW OR AUDIT FLOW
                            if (EditWorkflow == EditWorkflowType.Edit)
                            {
                                _SigningRulesControl = new SigningRules_Control(this);
                                _SigningRulesControl.Name = pageKey;
                                PageList.Add(_SigningRulesControl.Name);
                                Controls.Add(_SigningRulesControl);
                                _SigningRulesControl.BringToFront();
                                _SigningRulesControl.Focus();
                            }
                            else
                            {
                                // Go to build page and provide the SiPolicy object
                                pageKey = "Event Logs Build Page";
                                _BuildPage = new BuildPage(this);
                                _BuildPage.Name = pageKey;
                                PageList.Add(_BuildPage.Name);
                                Controls.Add(_BuildPage);
                                _BuildPage.BringToFront();
                                _BuildPage.Focus();
                                button_Next.Visible = false;
                                ProcessPolicy();
                            }
                        }

                        ShowControlPanel(sender, e);
                    }

                break;

            // 4th Page
            case 4:
                    // New policy workflow
                    if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.New)
                    {
                        // New Base
                        if (Policy._PolicyType == WDAC_Policy.PolicyType.BasePolicy)
                        {
                            // New BASE policy - Add file rules page
                            string pageKey = "SigningRulesPage";
                            if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, show instance
                            {
                                Control[] _Pages = Controls.Find(pageKey, true);
                                _Pages[0].Show();
                                _Pages[0].BringToFront();
                                _Pages[0].Focus();
                            }
                            else
                            {
                                _SigningRulesControl = new SigningRules_Control(this);
                                _SigningRulesControl.Name = pageKey;
                                PageList.Add(_SigningRulesControl.Name);
                                Controls.Add(_SigningRulesControl);
                                _SigningRulesControl.BringToFront();
                                _SigningRulesControl.Focus();
                            }

                            ShowControlPanel(sender, e);
                        }

                        // New Supplemental or AppId Tagging Policy
                        if (Policy._PolicyType == WDAC_Policy.PolicyType.SupplementalPolicy
                            || Policy._PolicyType == WDAC_Policy.PolicyType.AppIdTaggingPolicy)
                        {
                            // New SUPPLEMENTAL policy -- build out policy

                            string pageKey  = "BuildPage";
                            if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, show instance
                            {
                                Control[] _Pages = Controls.Find(pageKey, true);
                                _Pages[0].Show();
                                _Pages[0].BringToFront();
                                _Pages[0].Focus();
                            }
                            else
                            {
                                _BuildPage = new BuildPage(this);
                                _BuildPage.Name = pageKey;
                                PageList.Add(_BuildPage.Name);
                                Controls.Add(_BuildPage);
                                _BuildPage.BringToFront();
                                _BuildPage.Focus();
                                button_Next.Visible = false;
                            }
                            ShowControlPanel(sender, e);
                            ProcessPolicy();
                        }
                    }

                    // Edit policy workflow
                    else if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.Edit)
                    {
                        // Edit & view mode

                        button_Next.Visible = false;

                        string pageKey = "BuildPage";
                        if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, show instance
                        {
                            Control[] _Pages = Controls.Find(pageKey, true);
                            _Pages[0].Show();
                            _Pages[0].BringToFront();
                            _Pages[0].Focus();
                        }
                        else
                        {
                            _BuildPage = new BuildPage(this);
                            _BuildPage.Name = pageKey;
                            PageList.Add(_BuildPage.Name);
                            Controls.Add(_BuildPage);
                            _BuildPage.BringToFront();
                            _BuildPage.Focus();
                        }
                        ShowControlPanel(sender, e);
                        ProcessPolicy();
                    }

                break;

            // 5th Page
            case 5:
                if(Policy.PolicyWorkflow == WDAC_Policy.Workflow.New 
                    && Policy._PolicyType == WDAC_Policy.PolicyType.BasePolicy)
                {
                        // Build out the policy - begin PS execution

                        button_Next.Visible = false;

                        string pageKey = "BuildPage";
                        if (PageList.Contains(pageKey) && !RedoFlowRequired) //already been here, show instance
                        {
                            Control[] _Pages = Controls.Find(pageKey, true);
                            _Pages[0].Show();
                            _Pages[0].BringToFront();
                            _Pages[0].Focus();
                        }
                        else
                        {
                            _BuildPage = new BuildPage(this);
                            _BuildPage.Name = pageKey;
                            PageList.Add(_BuildPage.Name);
                            Controls.Add(_BuildPage);
                            _BuildPage.BringToFront();
                            _BuildPage.Focus();
                        }

                        ShowControlPanel(sender, e);
                        ProcessPolicy();
                }
            break;

            } // End of Switch
        }

        /// <summary>
        /// Begins executing the worker thread and creating the policy created by users. 
        /// Creates the temp directory path to write all of the temp data.  
        /// </summary>
        private void ProcessPolicy()
        {
            // Short circuit policy building if using Event Log workflow
            if(Policy.PolicyWorkflow == WDAC_Policy.Workflow.Edit 
                && EditWorkflow == EditWorkflowType.EventLog)
            {
                ProcessEventLogPolicy();
                return;
            }

            // Create folder for temp intermediate policies
            try
            {
                Directory.CreateDirectory(TempFolderPath); // Create new temp folder
            }
            catch (Exception e)
            {
                Logger.Log.AddErrorMsg("Process Policy() caught the following exception ", e);
            }
           
            Logger.Log.AddNewSeparationLine("Workflow -- Building Policy Underway"); 

            // Write all policy, file and signer rules to xml files:
            if (!backgroundWorker1.IsBusy)
                backgroundWorker1.RunWorkerAsync();
        }

        /// <summary>
        /// Event handler where the time-consuming work of creating the policies is accomplished. 
        /// No UI changes should be performed in this method. 
        /// </summary>
        private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            // Flag to skip policy conversion in the case of signed policy without PolicySigners section
            bool skipPolicyConversion = false; 
            
            // Use an SiPolicy object to store all new file and signer rules
            SiPolicy siPolicyNewRules = Helper.DeserializeXMLStringtoPolicy(Resources.EmptyWDAC);

            if (Policy.PolicyWorkflow != WDAC_Policy.Workflow.Merge)
            {
                // Add trust for Signed Scripts
                bool success = PSCmdlets.AddPSSigner();
                if (!success) {
                    ShowPSTrustFailureUI();
                }
                
                // Handle custom value rules: 
                siPolicyNewRules = ProcessCustomValueRules(worker, siPolicyNewRules);

                // Handle user created rules:
                siPolicyNewRules = ProcessSignerRules(worker, siPolicyNewRules);

                // Merge all of the unique CI policies with template and/or base policy:
                MergeTemplatesPolicy(siPolicyNewRules, worker);

                // Handle Policy Rule-Options
                SetPolicyRuleOptions(worker, ref skipPolicyConversion);

                // Handle COM rules
                CreateComObjectRules(worker);

                // Handle AppID Tags
                SetAppIDPolicyTags(worker);
                
                // Set additional parameters: policy name, GUIDs, version, etc
                SetAdditionalParameters(worker);
            }

            // Handle the merge workflow
            if(Policy.PolicyWorkflow == WDAC_Policy.Workflow.Merge)
            {
                MergePolicies_MergeControl(worker);
            }

            // Convert the policy from XML to Binary file
            if (Properties.Settings.Default.convertPolicyToBinary
                && !skipPolicyConversion)
            {
                Policy.BinPath = PSCmdlets.CompilePolicyBinary(Policy.SchemaPath);
            }

            worker.ReportProgress(100);
        }

        /// <summary>
        /// Event handler where the progress of the worker is updated when worker.ReportProgress is 
        /// called. Handle UI updates to BuildPage class through public method 'UpdateProgressBar'.
        /// </summary>
        private void BackgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string process = "";
            int progressPercent = e.ProgressPercentage;
            if (progressPercent <= 10)
                process = "Building policy rules ...";
            else if (progressPercent <= 70)
                process = "Configuring policy signer and file rules ...";
            else if (progressPercent <= 80)
                process = "Building custom policy file rules ...";
            else if (progressPercent <= 85)
                process = "Merging custom rules policies ...";
            else if (progressPercent <= 95)
                process = "Merging custom rules with template policies ...";
            else
                process = "Setting additional parameters ..."; 

            _BuildPage.UpdateProgressBar(e.ProgressPercentage, process);
        }

        /// <summary>
        /// This event handler deals with the results of the background operation. Handles both
        /// successful and unsuccessful results. Successful results in a call to the BuildPage 
        /// ShowFinishMsg method.
        /// </summary>
        private void BackgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // If error in workflow, log final exception
            if (e.Error != null)
            {
                Logger.Log.AddErrorMsg("ProcessPolicy() caught the following exception ", e.Error);
            }
            Logger.Log.AddNewSeparationLine("Workflow -- DONE");

            // Close the log file.At the end of the workflow
            Logger.Log.CloseLogger(); 

            // Check for error and show the error UI on the build page
            if (e.Error != null)
            {
                _BuildPage.ShowError();
            }
            else
            {
                _BuildPage.UpdateProgressBar(100, " ");

                // If we successfully converted the policy XML to binary, show both file paths
                // If conversion was unsuccessful, show only the XML path
                if (!String.IsNullOrEmpty(Policy.BinPath))
                {
                    _BuildPage.ShowFinishMsg(Policy.SchemaPath, Policy.BinPath);
                }
                else
                {
                    _BuildPage.ShowFinishMsg(Policy.SchemaPath); 
                }
            }

            // Re-initialize SiPolicy object
            Policy = new WDAC_Policy();
        }

        /// <summary>
        /// Create the policy rule-option pairings the user has set and creates a seperate CI policy just for the pairings. 
        /// Also removes all of the rule-options for the template policy such that there is no merge conflict. 
        /// </summary>
        public void SetPolicyRuleOptions(BackgroundWorker worker, ref bool skipPolicyConversion)
        {
            Logger.Log.AddInfoMsg("--- Create Policy Rule Options --- ");

            //Dictionary<string, Dictionary<string, string>>.KeyCollection keys = this.Policy.ConfigRules.Keys;
            SiPolicy finalPolicy = new SiPolicy();

            // Make a copy of the Template policy and REMOVE ALL of its rule options
            // That way we ensure we take the set rule-options from ConfigRules
            try
            {
                finalPolicy = Helper.DeserializeXMLtoPolicy(Policy.SchemaPath);
                if(finalPolicy == null)
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.Log.AddErrorMsg("Create Policy Rule Options -- parsing OutputSchema.xml encountered the following error ", e); 
            }

            // Policy Rule Options derived from the final state of the rules page
            List<RuleType> ruleOptionsList = Policy.PolicyRuleOptions;

            // Assert supplemental policies and legacy policies cannot have the Supplemental (rule #17) option
            if (Policy.HasRuleOption(OptionType.EnabledAllowSupplementalPolicies)
                && (Policy._Format == WDAC_Policy.Format.Legacy 
                    || Policy._PolicyType == WDAC_Policy.PolicyType.SupplementalPolicy))
            {
                Policy.RemoveRuleOption(OptionType.EnabledAllowSupplementalPolicies);
                Logger.Log.AddInfoMsg("Removing EnabledAllowSupplementalPolicies (rule-option 17)");
            }

            // Updated logic for Issue #262 - instead of forcing rule-option 6, skip binary conversion
            // if the policy has the signed CI policy option (rule #6) and lacks policy signers
            if (Properties.Settings.Default.convertPolicyToBinary
                && !Policy.HasRuleOption(OptionType.EnabledUnsignedSystemIntegrityPolicy))
            {
                if (finalPolicy.UpdatePolicySigners == null
                    || finalPolicy.UpdatePolicySigners.Length < 1)
                {
                    skipPolicyConversion = true;
                }

                // Handle Supplemental Signers issue - must have supplemental signers if signed policy && policy
                // allows supplemental policies
                if (Policy.HasRuleOption(OptionType.EnabledAllowSupplementalPolicies))
                {
                    if (finalPolicy.SupplementalPolicySigners == null
                    || finalPolicy.SupplementalPolicySigners.Length < 1)
                    {
                        skipPolicyConversion = true;
                    }
                }
            }

            // De-duplicate the Rule Options list
            List<RuleType> dedupedRuleOptions = PolicyHelper.DeDuplicateRuleOptions(ruleOptionsList);

            // Convert from List<RuleType> to RuleType[]
            RuleType[] ruleOptions = new RuleType[dedupedRuleOptions.Count];
            for(int i = 0; i< ruleOptions.Length; i++)
            {
                ruleOptions[i] = dedupedRuleOptions[i];
                Logger.Log.AddInfoMsg("Adding " + dedupedRuleOptions[i].Item); 
            }

            try
            {
                finalPolicy.Rules = ruleOptions;
                Helper.SerializePolicytoXML(finalPolicy, Policy.SchemaPath);
            }
            catch (Exception e)
            {
                Logger.Log.AddErrorMsg("Create Policy Rule Options -- serializing finalPolicy object back to OutputSchema.xml encountered the following error ", e);
            }

            worker.ReportProgress(95);
        }

        /// <summary>
        /// Creates the COM object rules and adds them to the policy
        /// </summary>
        /// <param name="worker"></param>
        public void CreateComObjectRules(BackgroundWorker worker)
        {
            // Iterate through all the custom rules for the COM object rules only
            List<COM> comObjectRules = new List<COM>();
            foreach(var rule in Policy.CustomRules)
            {
                if(rule.COMObject.Guid != null)
                {
                    comObjectRules.Add(rule.COMObject);
                    Logger.Log.AddInfoMsg(String.Format("Creating COM {0} Rule for Provider={1} and Key={2}",
                                                       rule.COMObject.ValueItem.ToString(),
                                                       rule.COMObject.Provider.ToString(),
                                                       rule.COMObject.Guid));
                }
            }

            // Return if no COM rules were created
            if(comObjectRules.Count == 0)
            {
                worker.ReportProgress(98); 
                return; 
            }

            // Manipulate the final policy on disk as oppposed to any temp policies
            SiPolicy policy = Helper.DeserializeXMLtoPolicy(Policy.SchemaPath);
            policy = PolicyHelper.CreateComRule(policy, comObjectRules);

            try
            {
                Helper.SerializePolicytoXML(policy, Policy.SchemaPath);
            }
            catch (Exception e)
            {
                Logger.Log.AddErrorMsg(String.Format("Exception encountered in CreateComObjectRules(): {0}", e));
            }

        }

        /// <summary>
        /// Creates all the AppID key-value pairs and adds them to the policy
        /// </summary>
        /// <param name="worker"></param>
        public void SetAppIDPolicyTags(BackgroundWorker worker)
        {
            // Skip if this is not an AppID Tagging Policy - no AppID Tags to process
            if(Policy._PolicyType != WDAC_Policy.PolicyType.AppIdTaggingPolicy)
            {
                worker.ReportProgress(99);
                return;
            }

            // Iterate through all the custom rules for the AppID Tags only
            List<AppID> appIDs = new List<AppID>();
            foreach (var rule in Policy.CustomRules)
            {
                if (!String.IsNullOrEmpty(rule.AppIDTag.Key))
                {
                    appIDs.Add(rule.AppIDTag);
                    Logger.Log.AddInfoMsg(String.Format("Creating AppID Tag with Key={0} and Key={1}",
                                                         rule.AppIDTag.Key,
                                                         rule.AppIDTag.Value));
                }
            }

            // Return if no AppID Tags were found
            if (appIDs.Count == 0)
            {
                worker.ReportProgress(99);
                return;
            }

            // Manipulate the final policy on disk as oppposed to any temp policies
            SiPolicy policy = Helper.DeserializeXMLtoPolicy(Policy.SchemaPath);
            policy = PolicyHelper.CreateAppIDTags(policy, appIDs);

            try
            {
                Helper.SerializePolicytoXML(policy, Policy.SchemaPath);
            }
            catch (Exception e)
            {
                Logger.Log.AddErrorMsg(String.Format("Exception encountered in CreateComObjectRules(): {0}", e));
            }

        }



        /// <summary>
        /// Sets the additonal parameters at the end of a policy: GUIDs, versions, etc
        /// </summary>
        void SetAdditionalParameters(BackgroundWorker worker)
        {
            // Operations:
            // 1. Set Policy GUIDs (PolicyID, BasePolicyID)
            // 2. Settings PolicyInfo.Name and PolicyInfo.Id
            // 3. VersionExpolicy 
            // 4. HVCI state
            // 5. Format IDs in the case of Issue #247

            Logger.Log.AddInfoMsg("-- Set Additional Parameters --");
            SiPolicy siPolicy = Helper.DeserializeXMLtoPolicy(Policy.SchemaPath); 

            if(siPolicy == null)
            {
                return;
            }

            // The only time we should be reseting the GUID is NEW multi-policy Base or Supplemental Policy
            if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.New)
            {
                // Set policy info - ID, Name
                // Serialize the policy back to XML so settings persist after GUID reset
                siPolicy.Settings = PolicyHelper.SetPolicyInfo(siPolicy.Settings, Policy.PolicyName, Policy.PolicyID);
                Helper.SerializePolicytoXML(siPolicy, Policy.SchemaPath); 

                if (Policy._PolicyType == WDAC_Policy.PolicyType.BasePolicy 
                    && Policy._Format == WDAC_Policy.Format.MultiPolicy)
                {
                    // Run Set-CIPolicyIdInfo -ResetPolicyID to force the policy into multiple policy format
                    PSCmdlets.ResetGuidPS(Policy.SchemaPath);

                    // Deserialize again since Wizard reset the policy on disk to multi-policy
                    siPolicy = Helper.DeserializeXMLtoPolicy(Policy.SchemaPath);

                    // Reset the GUIDs
                    siPolicy.BasePolicyID = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
                    siPolicy.PolicyID = siPolicy.BasePolicyID;

                    // Force SiPolicy PolicyType to BasePolicy
                    siPolicy.PolicyType = global::PolicyType.BasePolicy; 
                    siPolicy.PolicyTypeSpecified = true;

                    // Remove PolicyTypeID, if applicable
                    siPolicy.PolicyTypeID = null;
                }

                // If single policy/legacy policy, ensure the correct IDs are set on the policy
                else if (Policy._Format == WDAC_Policy.Format.Legacy)
                {
                    siPolicy.BasePolicyID = null;
                    siPolicy.PolicyID = null;
                    siPolicy.PolicyTypeSpecified = false; 
                    siPolicy.PolicyTypeID = Resources.LegacyPolicyID_GUID; 
                }

                // AppID Tagging Policy
                else if(Policy._PolicyType == WDAC_Policy.PolicyType.AppIdTaggingPolicy)
                {
                    // Reset the GUIDs
                    siPolicy.BasePolicyID = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
                    siPolicy.PolicyID = siPolicy.BasePolicyID;

                    // Force SiPolicy PolicyType to BasePolicy
                    siPolicy.PolicyType = global::PolicyType.AppIDTaggingPolicy;
                    siPolicy.PolicyTypeSpecified = true;
                }

                // If supplemental policy, set the Base policy guid; reset the policy ID and set the policy name
                else if (Policy._PolicyType == WDAC_Policy.PolicyType.SupplementalPolicy)
                {
                    // User provided path to base policy XML
                    if(Policy.BasePolicyId == Guid.Empty)
                    {
                        // Set BasePolicyID to match the Id of the linked base policy
                        siPolicy.BasePolicyID = Helper.DeserializeXMLtoPolicy(Policy.BaseToSupplementPath).BasePolicyID;
                    }

                    // User provided the GUID of the base policy only
                    else
                    {
                        string baseId = Policy.BasePolicyId.ToString().ToUpper();

                        // Set BasePolicyID to match the Id provided base policy ID
                        if (baseId.Contains("{"))
                        {
                            siPolicy.BasePolicyID = baseId; 
                        }
                        else
                        {
                            siPolicy.BasePolicyID = "{" + baseId + "}";
                        }
                    }

                    // Reset the PolicyID guid so it is unique
                    siPolicy.PolicyID = "{" + Guid.NewGuid().ToString().ToUpper() + "}";

                    // Force SiPolicy PolicyType to Supplemental
                    siPolicy.PolicyType = global::PolicyType.SupplementalPolicy;
                    siPolicy.PolicyTypeSpecified = true;

                    // Remove PolicyTypeID, if applicable
                    siPolicy.PolicyTypeID = null;
                }

                // Log information set for debugging
                Logger.Log.AddInfoMsg("Additional parameters set - BasePolicyID: " + siPolicy.BasePolicyID);
                Logger.Log.AddInfoMsg("Additional parameters set - PolicyID: " + siPolicy.PolicyID);

                // Log Policy settings set by the Wizard
                foreach(var setting in siPolicy.Settings)
                {
                    Logger.Log.AddInfoMsg(String.Format("Additional parameters set - Provider:{0} - Key:{1} - ValueName: {2} - Value:{3} ", 
                        setting.Provider, setting.Key, setting.ValueName, setting.Value.Item));
                }
            }

            // Update the version number on the edited policies. If not specified, version defaults to 10.0.0.0
            if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.Edit)
            {
                // Set policy info - ID, Name
                siPolicy.Settings = PolicyHelper.SetPolicyInfo(siPolicy.Settings, Policy.PolicyName, Policy.PolicyID); 
                siPolicy.VersionEx = Policy.VersionNumber;

                Logger.Log.AddInfoMsg("Additional parameters set - Info.PolicyName to " + siPolicy.Settings[0].Value.Item);
                Logger.Log.AddInfoMsg("Additional parameters set - Info.PolicyID to " + siPolicy.Settings[1].Value.Item);
                Logger.Log.AddInfoMsg("Additional parameters set - VersionEx " + siPolicy.VersionEx);
            }

            // Set HVCI state
            if (Policy.EnableHVCI)
            {
                // Enable HVCI since HVCI is not a Rule-Option
                siPolicy.HvciOptions = 1;
                Logger.Log.AddInfoMsg("Additional parameters set - HVCI set to 1");
            }
            else
            {
                siPolicy.HvciOptions = 0;
                Logger.Log.AddInfoMsg("Additional parameters set - HVCI set to 0");
            }

            try
            {
                Helper.SerializePolicytoXML(siPolicy, Policy.SchemaPath); 
            }
            catch (Exception e)
            {
                Logger.Log.AddErrorMsg(String.Format("Exception encountered in SetAdditionalParameters(): {0}", e));
            }
        }

        /// <summary>
        /// Processes all of the non-custom signer rules defined by user. 
        /// </summary>
        public SiPolicy ProcessSignerRules(BackgroundWorker worker, SiPolicy siPolicy)
        {
            List<string> customRulesPathList = new List<string>();
            int nCustomRules = Policy.CustomRules.Count;
            int progressVal = 0;

            // Iterate through all of the custom rules and update the progress bar    
            for (int i = 0; i < nCustomRules; i++)
            {
                progressVal = 25 + i * 60 / nCustomRules;
                worker.ReportProgress(progressVal); //Assumes the operations involved with this step take about 70% -- probably should be a little higher

                var customRule = Policy.CustomRules[i];

                // Skip; already handled ALL custom value rules
                if(customRule.UsingCustomValues)
                {
                    continue;
                }

                // Skip the following rules that are handled by custom rules method -
                // File Attributes, PFN rules, file/folder path rules
                if (!(customRule.Type == PolicyCustomRules.RuleType.Publisher 
                      || customRule.Type == PolicyCustomRules.RuleType.Hash
                      || customRule.Type == PolicyCustomRules.RuleType.FolderScan
                      || customRule.Type == PolicyCustomRules.RuleType.Certificate))
                {
                    continue;
                }

                string tmpPolicyPath = Helper.GetUniquePolicyPath(TempFolderPath);

                // Create a single policy per rule using the Powershell cmdlets with Level=PCACertificate or Publisher
                // and add the additional attributes like OriginalFilename and version by serializing the
                // policy to reduce the complexity of the PS scripting
                if (customRule.Type == PolicyCustomRules.RuleType.Publisher)
                {
                    SiPolicy signerSiPolicy = PSCmdlets.CreatePolicyRuleFromPS(customRule, tmpPolicyPath);

                    if (signerSiPolicy != null)
                    {
                        signerSiPolicy = PolicyHelper.AddSignerRuleAttributes(customRule, signerSiPolicy);
                        siPolicy = PolicyHelper.MergePolicies(signerSiPolicy, siPolicy);    
                    }
                }
                
                // Hash Rules -- Invoke Powershell cmd to generate 
                if(customRule.Type == PolicyCustomRules.RuleType.Hash)
                {
                    SiPolicy signerSiPolicy = PSCmdlets.CreatePolicyRuleFromPS(customRule, tmpPolicyPath);

                    if (signerSiPolicy != null)
                    {
                        siPolicy = PolicyHelper.MergePolicies(signerSiPolicy, siPolicy);
                    }
                }

                // Folder Scan -- Invoke the New-CiPolicy PS cmd to generate a CI policy
                if(customRule.Type == PolicyCustomRules.RuleType.FolderScan)
                {
                    SiPolicy signerSiPolicy; 
                    if (Policy._PolicyType == WDAC_Policy.PolicyType.BasePolicy)
                    {
                        signerSiPolicy = PSCmdlets.CreateScannedPolicyFromPS(customRule, tmpPolicyPath);
                    }
                    else
                    {
                        signerSiPolicy = PSCmdlets.CreateScannedPolicyFromPS(customRule, tmpPolicyPath, Policy.BaseToSupplementPath);
                    }
                    
                    // Successful Scan completed
                    if (signerSiPolicy != null)
                    {
                        siPolicy = PolicyHelper.MergePolicies(signerSiPolicy, siPolicy);
                    }
                }

                // Certificate File Rule -- Invoke Add-SignerRule PS Cmd to generate a policy
                if(customRule.Type == PolicyCustomRules.RuleType.Certificate)
                {
                    // Copy EmptyWDAC.xml to tmpPolicyPath so Add-SignerRule has a policy which the signer rules can be added
                    File.Copy(Path.Combine(ExeFolderPath, "Templates", "EmptyWDAC.xml"), tmpPolicyPath);

                    SiPolicy signerSiPolicy = PSCmdlets.AddSignerRuleFromPS(customRule, tmpPolicyPath);

                    // Successful Scan completed
                    if (signerSiPolicy != null)
                    {
                        siPolicy = PolicyHelper.MergePolicies(signerSiPolicy, siPolicy);
                    }
                }
            }

            return siPolicy;
        }

        /// <summary>
        /// Processes all of the custom rules with user input fields
        /// </summary>
        /// <param name="worker"></param>
        public SiPolicy ProcessCustomValueRules(BackgroundWorker worker, SiPolicy siPolicyCustomValueRules)
        {
            // Iterate through all of the custom rules and PFN rules and update the progress bar    
            foreach (var customRule in Policy.CustomRules)
            {
                if(customRule.UsingCustomValues)
                {
                    siPolicyCustomValueRules = HandleCustomValues(customRule, siPolicyCustomValueRules);
                    nCustomValueRules++;
                }
                else if(customRule.Type == PolicyCustomRules.RuleType.PackagedApp)
                {
                    siPolicyCustomValueRules = PolicyHelper.CreatePFNRule(customRule, siPolicyCustomValueRules);
                    nCustomValueRules++;
                }
                else if(customRule.Type == PolicyCustomRules.RuleType.FileAttributes)
                {
                    siPolicyCustomValueRules = PolicyHelper.CreateNonCustomFileAttributeRule(customRule, siPolicyCustomValueRules);
                    nCustomValueRules++;
                }
                else if(customRule.Type == PolicyCustomRules.RuleType.FilePath || customRule.Type == PolicyCustomRules.RuleType.FolderPath)
                {
                    siPolicyCustomValueRules = PolicyHelper.CreateNonCustomFilePathRule(customRule, siPolicyCustomValueRules);
                    nCustomValueRules++;
                }
            }

            worker.ReportProgress(25);
            return siPolicyCustomValueRules;
        }

        /// <summary>
        /// Processes all of the custom rules with custom values. E.g. custom version ranges, custom filenames, file paths
        /// </summary>
        public SiPolicy HandleCustomValues(PolicyCustomRules customRule, SiPolicy siPolicy)
        {
            if(customRule.Type == PolicyCustomRules.RuleType.Publisher)
            {
                siPolicy = PolicyHelper.CreateFilePublisherRule(customRule, siPolicy);
            }

            else if (customRule.Type == PolicyCustomRules.RuleType.FileAttributes)
            {
                if (customRule.Permission == PolicyCustomRules.RulePermission.Allow)
                {
                    siPolicy = PolicyHelper.CreateAllowFileAttributeRule(customRule, siPolicy);
                }
                else
                {
                    siPolicy = PolicyHelper.CreateDenyFileAttributeRule(customRule, siPolicy);
                }
            }

            else if(customRule.Type == PolicyCustomRules.RuleType.PackagedApp)
            {
                siPolicy = PolicyHelper.CreatePFNRule(customRule, siPolicy);
            }

            else if (customRule.Type == PolicyCustomRules.RuleType.FilePath || customRule.Type == PolicyCustomRules.RuleType.FolderPath)
            {
                if (customRule.Permission == PolicyCustomRules.RulePermission.Allow)
                {
                    siPolicy = PolicyHelper.CreateAllowPathRule(customRule, siPolicy);
                }
                else
                {
                    siPolicy = PolicyHelper.CreateDenyPathRule(customRule, siPolicy);
                }
            }

            else if (customRule.Type == PolicyCustomRules.RuleType.Hash)
            {
                if (customRule.Permission == PolicyCustomRules.RulePermission.Allow)
                {
                    siPolicy = PolicyHelper.CreateAllowHashRule(siPolicy, customRule);
                }
                else
                {
                    siPolicy = PolicyHelper.CreateDenyHashRule(siPolicy, customRule);
                }
            }

            return siPolicy;
        }

        /// <summary>
        /// Merges the template policy, supplemental policy, and/or custom rules policy into the user's desired output path
        /// </summary>
        public void MergeTemplatesPolicy(SiPolicy siPolicyCustomRules, BackgroundWorker worker)
        {
            // Template policy @ this.TemplatePath, Supplemental policy @ this.BaseToSupplementPath
            // Operations: Merge template (always applicable) with suppleme (if applicable) with 
            //             merge results from MergeCustomRulesPolicy (if applicable) into policy 
            //             defined by user: Path: this.SchemaPath

            // If Edit mode is selected, since the 'New Save Location' field is optional, the Policy.SchemaPath can either be set or null.  
            // If the field is null, set the save locaiton to the same location as the EditPolicyPath.
            // Otherwise, keep Policy.SchemaPath as is
            
            Logger.Log.AddInfoMsg("--- Merge Templates Policy ---");

            // Check whether the User Mode recommended block list rules are wanted in the output:
            if (Policy.UseUserModeBlocks)
            {
                // Issue #393 - some users cannot access the Blocklists file from \Program Files\WindowsApps so the Merge cmd throws an error
                // Instead, copy to the temp folder 
                string umBlocklist_src = Path.Combine(ExeFolderPath, "Templates", "Recommended_UserMode_Blocklist.xml");
                string umBlocklist_dst = Path.Combine(TempFolderPath, "Recommended_UserMode_Blocklist.xml");
                File.Copy(umBlocklist_src, umBlocklist_dst, true);

                siPolicyCustomRules = PolicyHelper.MergePolicies(siPolicyCustomRules, Helper.DeserializeXMLtoPolicy(umBlocklist_dst));
            }

            // Check whether the Kernel Mode recommended driver block list rules are wanted in the output:
            if (Policy.UseKernelModeBlocks)
            {
                // Issue #393 - some users cannot access the Blocklists file from \Program Files\WindowsApps so the Merge cmd throws an error
                // Instead, copy to the temp folder 
                string kmBlocklist_src = Path.Combine(ExeFolderPath, "Templates", "Recommended_Driver_Blocklist.xml");
                string kmBlocklist_dst = Path.Combine(TempFolderPath, "Recommended_Driver_Blocklist.xml");
                File.Copy(kmBlocklist_src, kmBlocklist_dst, true);

                siPolicyCustomRules = PolicyHelper.MergePolicies(siPolicyCustomRules, Helper.DeserializeXMLtoPolicy(kmBlocklist_dst));
            }


            if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.Edit)
            {
                if(String.IsNullOrEmpty(Policy.SchemaPath))
                {
                    // Since we don't have a new save location, copy the TemplatePath
                    // and append '_version' to the file path.
                    // Check if _v10.0.x.y is already in string ie. editing the output of an editing workflow
                    int versionNumPos = Policy.EditPathContainsVersionInfo(); 
                    if (versionNumPos > 0)
                    {
                        string filePathWithoutVer = Policy.EditPolicyPath.Substring(0, versionNumPos);
                        Policy.SchemaPath = String.Format("{0}_v{1}.xml", filePathWithoutVer, Policy.UpdateVersion());
                    }
                    else
                    {
                        string filePathWithoutExt = Path.Combine(Path.GetDirectoryName(Policy.EditPolicyPath), 
                                                                 Path.GetFileNameWithoutExtension(Policy.EditPolicyPath));
                        Policy.SchemaPath = String.Format("{0}_v{1}.xml", filePathWithoutExt, Policy.UpdateVersion());
                    }
                }
                else
                {
                    Policy.UpdateVersion(); 
                }

                // If editing policy, ensure that the IDs in the siPolicyCustomRules are not shared by the rules in Policy.siPolicy
                // If they are, update the IDs in siPolicyCustomRules so IDs in the policy under edit don't update causing Git issues for CX
                // New policies do not have this issue as they are guaranteed to be unique against the templates

                siPolicyCustomRules = PolicyHelper.RemapIDs(siPolicyCustomRules, Policy.siPolicy);
            }

            // New Policies:
            // Base Policy - policy under construction in this.SiPolicy 
            // Supplemental Policy: no existing rules

            // Edit Policies - current on disk policy in this.SiPolicy
            SiPolicy siPolicyFinal = PolicyHelper.MergePolicies(siPolicyCustomRules, Policy.siPolicy);

            // Write to output schema location

            // Check if the chosen path is user-writeable
            // If it is not, default to AppDataLocal\Temp\WDACWizard
            if (!Helper.IsUserWriteable(Path.GetDirectoryName(Policy.SchemaPath)))
            {
                Logger.Log.AddWarningMsg($"User-defined schema path is not user-writeable: {Policy.SchemaPath}");
                Policy.SchemaPath = Path.Combine(TempFolderPath, Path.GetFileName(Policy.SchemaPath));
                Logger.Log.AddWarningMsg($"Using Wizard defined schema path under temp folder: {Policy.SchemaPath}");
            }

            Helper.SerializePolicytoXML(siPolicyFinal, Policy.SchemaPath);
            worker.ReportProgress(90);
        }

        /// <summary>
        /// Calls the PS Cmd to merge all of the policies
        /// </summary>
        /// <param name="worker"></param>
        public void MergePolicies_MergeControl(BackgroundWorker worker)
        {
            // Input: Merge all of the policies on disk defined in this.PoliciesToMerge
            // Output: this.SchemaPath

            // Check if the chosen path is user-writeable
            // If it is not, default to AppDataLocal\Temp\WDACWizard
            if (!Helper.IsUserWriteable(Path.GetDirectoryName(Policy.SchemaPath)))
            {
                Logger.Log.AddWarningMsg($"User-defined schema path is not user-writeable: {Policy.SchemaPath}");
                Policy.SchemaPath = Path.Combine(TempFolderPath, Path.GetFileName(Policy.SchemaPath));
                Logger.Log.AddWarningMsg($"Using Wizard defined schema path under temp folder: {Policy.SchemaPath}");
            }

            PSCmdlets.MergePolicies(Policy.PoliciesToMerge, Policy.SchemaPath);
            worker.ReportProgress(90);
        }

        /// <summary>
        /// Workflow to create the event log policy xml and compile to binary
        /// </summary>
        internal void ProcessEventLogPolicy()
        {
            // Save the path under the Downloads folder 
            string fileName = $"EventLogPolicy_{Helper.GetFormattedDateTime()}.xml";
            string pathToWrite = Path.Combine(Helper.GetDocumentsFolder(), fileName);

            // Issue # 392 - Reset the policy GUID
            PolicyHelper.ResetPolicyGuid(EventLogPolicy);

            try
            {
                Helper.SerializePolicytoXML(EventLogPolicy, pathToWrite);
            }
            catch (Exception e)
            {
                Logger.Log.AddErrorMsg("Event Log SiPolicy serialization failed with error: ", e);
            }

            // Compile supplemental policies as they now have an accurate Base ID and unique Policy ID
            // Convert the policy from XML to Binary file
            if (Properties.Settings.Default.convertPolicyToBinary)
            {
                Policy.BinPath = PSCmdlets.CompilePolicyBinary(pathToWrite);

                // Build Page:
                _BuildPage.UpdateProgressBar(100, "Finished completing event log conversion to App Control Policy");
                _BuildPage.ShowFinishMsg(pathToWrite, Policy.BinPath);
            }
            else
            {
                // Build Page:
                _BuildPage.UpdateProgressBar(100, "Finished completing event log conversion to App Control Policy");
                _BuildPage.ShowFinishMsg(pathToWrite);
            }

            // Close log file 
            Logger.Log.CloseLogger();
        }

        // UI helper functions

        /// <summary>
        /// Prompts the user to confirm if they want to abandon progress
        /// </summary>       
        private bool WantToAbandonWork()
        {
            Logger.Log.AddWarningMsg("Abandon Work Entered.");
            DialogResult res = MessageBox.Show("Are you sure you want to abandon your progress?", 
                                                "Confirmation", 
                                                MessageBoxButtons.OKCancel, 
                                                MessageBoxIcon.Information);

            if (res == DialogResult.OK)
            {
                Logger.Log.AddWarningMsg("Abandon Work returned OK.");
                return true;
            }
            else if (res == DialogResult.Cancel)
            {
                Logger.Log.AddWarningMsg("Abandon Work returned Cancel.");
                return false;
            }
            else
            {
                Logger.Log.AddWarningMsg("Abandon Work returned Other.");
                return false;
            }       
        }

        /// <summary>
        /// Displays the left-most control panel with home, settings buttons and progress pages. Displays the highlighting panel to show users current page.
        /// </summary> 
        private void ShowControlPanel(object sender, EventArgs e)
        {
            Controls.Add(control_Panel);
            control_Panel.BringToFront();
            control_Panel.Focus();

            Controls.Add(button_Next);
            button_Next.BringToFront();
            button_Next.Focus(); 

            // Set highlight panel location
            int X_OFFSET = 15;
            int Y_OFFSET = 5;
            try
            {
                var sideButton = (Button)sender; 
                controlHighlight_Panel.Location = new Point(sideButton.Location.X - X_OFFSET, sideButton.Location.Y + Y_OFFSET);
            }
            catch(Exception exc)
            {
                controlHighlight_Panel.Location = new Point(home_Button.Location.X - X_OFFSET, home_Button.Location.Y + Y_OFFSET);
            }

            // Enable Settings Button -- if on building page, it will be disabled below
            settings_Button.Enabled = true; 

            // Set link text
            if(Policy.PolicyWorkflow == WDAC_Policy.Workflow.New)
            {
                if(Policy._PolicyType == WDAC_Policy.PolicyType.BasePolicy)
                {
                    workflow_Label.Visible = true;
                    page1_Button.Visible = true;
                    page2_Button.Visible = true;
                    page3_Button.Visible = true;
                    page4_Button.Visible = true;
                    page5_Button.Visible = true;
                    workflow_Label.Text = "Policy Creator";
                    page1_Button.Text = "Policy Type";
                    page2_Button.Text = "Policy Template";
                    page3_Button.Text = "Policy Rules";
                    page4_Button.Text = "File Rules";
                    page5_Button.Text = "Creating Policy";
                }

                else if (Policy._PolicyType == WDAC_Policy.PolicyType.SupplementalPolicy)
                {
                    workflow_Label.Visible = true;
                    page1_Button.Visible = true;
                    page2_Button.Visible = true;
                    page3_Button.Visible = true;
                    page4_Button.Visible = true;
                    page5_Button.Visible = false;

                    workflow_Label.Text = "Policy Creator";
                    page1_Button.Text = "Policy Type";
                    page2_Button.Text = "Policy Rules";
                    page3_Button.Text = "File Rules";
                    page4_Button.Text = "Creating Policy";
                }
            }
            // Policy Editor
            else if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.Edit)
            {
                // Editing a policy but not creating one from event logs
                if(EditWorkflow == EditWorkflowType.Edit)
                {
                    workflow_Label.Visible = true;
                    page1_Button.Visible = true;
                    page2_Button.Visible = true;
                    page3_Button.Visible = true;
                    page4_Button.Visible = true;
                    page5_Button.Visible = false;

                    workflow_Label.Text = "Policy Editor";
                    page1_Button.Text = "Select Policy";
                    page2_Button.Text = "Policy Rules";
                    page3_Button.Text = "File Rules";

                    page4_Button.Text = "Creating Policy";
                }
                // Creating a policy from event log or advanced hunting
                else if(EditWorkflow == EditWorkflowType.EventLog)
                {
                    workflow_Label.Visible = true;
                    page1_Button.Visible = true;
                    page2_Button.Visible = true;
                    page3_Button.Visible = true;
                    page4_Button.Visible = false;
                    page5_Button.Visible = false;

                    workflow_Label.Text = "Policy Editor";
                    page1_Button.Text = "Select Policy";
                    page2_Button.Text = "File Rules";
                    page3_Button.Text = "Creating Policy";
                }
            }
            
            // Policy Merger
            else if(Policy.PolicyWorkflow == WDAC_Policy.Workflow.Merge)
            {
                workflow_Label.Visible = true;
                page1_Button.Visible = true;
                page2_Button.Visible = true;
                page3_Button.Visible = false;
                page4_Button.Visible = false;
                page5_Button.Visible = false;

                workflow_Label.Text = "Policy Merger";
                page1_Button.Text = "Select Policies";
                page2_Button.Text = "Creating Policy";
            }
            else
            {
                workflow_Label.Visible = false;
                page1_Button.Visible = false;
                page2_Button.Visible = false;
                page3_Button.Visible = false;
                page4_Button.Visible = false;
                page5_Button.Visible = false;
            }

            // Lazy implementation of highlighting current page in workflow
            switch(CurrentPage){
            case 1:
                page1_Button.Enabled = true;
                page2_Button.Enabled = false;
                page3_Button.Enabled = false;
                page4_Button.Enabled = false;
                page5_Button.Enabled = false;
                controlHighlight_Panel.Location = new Point(page1_Button.Location.X - X_OFFSET, page1_Button.Location.Y + Y_OFFSET);
                break;

            case 2:
                if(view == 3) // Merge
                {
                    // Building page
                    page1_Button.Enabled = false;
                    page2_Button.Enabled = false;
                    page3_Button.Enabled = false;
                    page4_Button.Enabled = false;
                    page5_Button.Enabled = false;

                    settings_Button.Enabled = false; // disable settings button to prevent writing to flushed/closed logger
                    }
                else
                {
                    page1_Button.Enabled = true;
                    page2_Button.Enabled = true;
                    page3_Button.Enabled = false;
                    page4_Button.Enabled = false;
                    page5_Button.Enabled = false;
                }
                controlHighlight_Panel.Location = new Point(page2_Button.Location.X - X_OFFSET, page2_Button.Location.Y + Y_OFFSET);
                break;

            case 3:

                if(view ==2 && EditWorkflow == EditWorkflowType.EventLog) // event log or AH building page
                {
                    page1_Button.Enabled = false;
                    page2_Button.Enabled = false;
                    page3_Button.Enabled = false;
                    page4_Button.Enabled = false;
                    page5_Button.Enabled = false;

                    settings_Button.Enabled = false; // disable settings button to prevent writing to flushed/closed logger
                }
                else
                {
                    page1_Button.Enabled = true;
                    page2_Button.Enabled = true;
                    page3_Button.Enabled = true;
                    page4_Button.Enabled = false;
                    page5_Button.Enabled = false;
                }
                controlHighlight_Panel.Location = new Point(page3_Button.Location.X - X_OFFSET, page3_Button.Location.Y + Y_OFFSET);
                break;

            case 4:
                if(view == 2) // Edit
                {
                    // Building page
                    page1_Button.Enabled = false;
                    page2_Button.Enabled = false;
                    page3_Button.Enabled = false;
                    page4_Button.Enabled = false;
                    page5_Button.Enabled = false;

                    settings_Button.Enabled = false; // disable settings button to prevent writing to flushed/closed logger
                }
                else
                {
                    if(Policy._PolicyType == WDAC_Policy.PolicyType.SupplementalPolicy) // build page for supplemental policies
                    {
                        page1_Button.Enabled = false;
                        page2_Button.Enabled = false;
                        page3_Button.Enabled = false;
                        page4_Button.Enabled = false;
                        page5_Button.Enabled = false;

                        settings_Button.Enabled = false; // disable settings button to prevent writing to flushed/closed logger
                    }

                    else
                    {
                        page1_Button.Enabled = true;
                        page2_Button.Enabled = true;
                        page3_Button.Enabled = true;
                        page4_Button.Enabled = true;
                        page5_Button.Enabled = false;
                    }
                }
                    
                controlHighlight_Panel.Location = new Point(page4_Button.Location.X - X_OFFSET, page4_Button.Location.Y + Y_OFFSET);
                break;

            case 5:
                // Building page

                page1_Button.Enabled = false;
                page2_Button.Enabled = false;
                page3_Button.Enabled = false;
                page4_Button.Enabled = false;
                page5_Button.Enabled = false;

                settings_Button.Enabled = false;

                controlHighlight_Panel.Location = new Point(page5_Button.Location.X - X_OFFSET, page5_Button.Location.Y + Y_OFFSET);
                break;
            }
        }

        /// <summary>
        /// Empties this.Controls based on contents of PageList.
        /// </summary> 
        private void RemoveControls()
        {
            // Empty this.PageList so that we can start a new workflow after policy build
            foreach (var page in PageList)
            {
                Controls.RemoveByKey(page);
            }
            PageList.Clear(); 
        }

        /// <summary>
        /// Public method to set the text and visibility of the info text label at bottom-left of the form or user control.
        /// </summary>
        public void DisplayInfoText(int infoN)
        {
            label_Info.Visible = true;
            label_Info.ForeColor = Color.DeepSkyBlue;

            switch (infoN){
            case 0:
                // Reset label
                label_Info.Text = " ";
                HideInfoLabel();
                break;

            case 1:
                label_Info.Text = "Windows S Mode Mode does the following";
                break;

            case 2:
                label_Info.Text = "Signed and Reputable Mode does the following";
                break;

            case 3:
                label_Info.Text = "Audit Only Mode does the following";
                break;
                
            case 97:
                label_Info.Text = "Please select an exisiting policy .xml file before continuing.";
                label_Info.ForeColor = Color.Red;
                Logger.Log.AddWarningMsg("Page controller triggered without selecting existing policy .xml file"); 
                break;

            case 98:
                label_Info.Text = "Please select one of the options before continuing.";
                label_Info.ForeColor = Color.Red;
                Logger.Log.AddWarningMsg("Page controller triggered without selecting a policy template");
                break;

            case 99:

                label_Info.Text = ErrorMsg;
                label_Info.ForeColor = Color.Red;
                Logger.Log.AddErrorMsg(ErrorMsg);
                    break;

            default:
                label_Info.Text = " ";
                break;
            }

            label_Info.Focus();
            label_Info.BringToFront();
        }

        /// <summary>
        /// Sets the Control Panel and children UI elements
        /// depending on the state of Light or Dark mode
        /// </summary>
        public void SetControlPanelUI()
        {
            // Dark Mode
            if(Properties.Settings.Default.useDarkMode)
            {
                // Control panel color
                control_Panel.BackColor = Color.Black; 

                // Subcontrol elements like buttons and text
                foreach (Control control in control_Panel.Controls)
                {
                    if (control.Tag == null
                        || control.Tag.ToString() != Resources.IgnoreDarkModeTag)
                    {
                        control.ForeColor = Color.White;
                    }
                }

                // Home and Settings images
                settings_Button.Image = Resources.white_gear;
                home_Button.Image = Resources.white_house; 
            }

            // Light Mode
            else
            {
                // Control Panel color
                control_Panel.BackColor = Color.FromArgb(230, 230, 230); 

                // Subcontrol elements like buttons and text
                foreach(Control control in control_Panel.Controls)
                {
                    if(control.Tag == null 
                        || control.Tag.ToString() != Resources.IgnoreDarkModeTag)
                    {
                        control.ForeColor = Color.Black; 
                    }
                }

                // Home and Settings images
                settings_Button.Image = Resources.gear;
                home_Button.Image = Resources.house;
            }
        }

        /// <summary>
        /// Sets the colors for the Next Button which depends on the 
        /// state of Light and Dark Mode
        /// </summary>
        public void SetNextButtonUI()
        {
            // Dark Mode
            if (Properties.Settings.Default.useDarkMode)
            {
                button_Next.FlatAppearance.BorderColor = Color.DodgerBlue;
                button_Next.FlatAppearance.MouseDownBackColor = Color.FromArgb(50,30,144,255);
                button_Next.FlatAppearance.MouseOverBackColor = Color.FromArgb(50,30,144,255);
                button_Next.FlatStyle = FlatStyle.Flat;
                button_Next.ForeColor = Color.DodgerBlue;
                button_Next.BackColor = Color.Transparent;
            }

            // Light Mode
            else
            {
                button_Next.FlatAppearance.BorderColor = Color.Black;
                button_Next.FlatAppearance.MouseDownBackColor = Color.FromArgb(50,30,144,255);
                button_Next.FlatAppearance.MouseOverBackColor = Color.FromArgb(50,30,144,255);
                button_Next.FlatStyle = FlatStyle.Flat;
                button_Next.ForeColor = Color.Black;
                button_Next.BackColor = Color.WhiteSmoke;
            }
        }

        /// <summary>
        /// Shows message to user that script signer was not added to Trusted Store
        /// They will have to manually do that
        /// </summary>
        private void ShowPSTrustFailureUI()
        {
            // Break early if setting is disabled. The user has already seen this message and elected not to show this
            if (!Properties.Settings.Default.showIntegrityIssue)
            {
                return; 
            }

            DialogResult res = MessageBox.Show("The Wizard was unable to add trust for required PowerShell scripts. This may lead to policy build hanging during Folder Scanning. " +
                                                "To fix this issue, you must add the signing certificate to the Current User's Trusted Publisher Store. \r\n\r\n" +
                                                "Do you want to continue receiving this message on future failures?",
                                                "Wizard Integrity Issue",
                                                MessageBoxButtons.YesNo,
                                                MessageBoxIcon.Question);

            if (res == DialogResult.No)
            {
                Properties.Settings.Default.showIntegrityIssue = false; 
                Properties.Settings.Default.Save();
            }
        }

        /// <summary>
        /// Resets the logic flow to the home page by calling the Home Button Click method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ResetWorkflow(object sender, EventArgs e)
        {
            Home_Button_Click(sender, e); 
        }
    
        /// <summary>
        /// Creates the session folder. Contents such as the log, intermediate policies are stored here.
        /// </summary>
        private string CreateTempFolder()
        {
            //AppData + WDAC Temp folder
            string tempFolderPath = Helper.GetTempFolderPath(); 

            if (!Directory.Exists(tempFolderPath))
            {
                Directory.CreateDirectory(tempFolderPath);
            }

            return tempFolderPath; 
        }

        /// <summary>
        /// Triggered upon application closing event. 
        /// /// </summary>
        private void FormClosing_Event(object sender, FormClosingEventArgs e)
        {
            Logger.Log.CloseLogger();
        }

        /// <summary>
        /// Hide info label on timer tick
        /// /// </summary>
        public void HideInfoLabel()
        {
            label_Info.Visible = false;
        }

        /// <summary>
        /// Runs on page load. Sets the colors for the UI elements 
        /// depending on the state of Light and Dark Mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_Load(object sender, EventArgs e)
        {
            // Set background color
            SetMainWindowColors(); 

            // Set UI for the Control Panel
            SetControlPanelUI();

            // Set UI for the 'Next' Button
            SetNextButtonUI(); 
        }

        /// <summary>
        /// Sets the Main Windows UI colors depending on the 
        /// state of Light and Dark Mode
        /// </summary>
        public void SetMainWindowColors()
        {
            // Iterate over all the controls and set the forecolor
            foreach(Control control in Controls)
            {
                if(control.GetType() != typeof(Panel)) // exclude the control panel
                {
                    if(control.Tag == null 
                        || control.Tag.ToString() != Resources.IgnoreDarkModeTag)
                    {
                        if(Properties.Settings.Default.useDarkMode)
                        {
                            // Dark Mode
                            control.ForeColor = Color.White;
                        }
                        else
                        {
                            // Light Mode
                            control.ForeColor = Color.Black; 
                        }
                    }
                }
            }

            // Set the back color of the Main Window form
            if (Properties.Settings.Default.useDarkMode)
            {
                // Dark Mode
                BackColor = Color.FromArgb(15, 15, 15); 
            }
            else
            {
                // Light Mode
                BackColor = Color.White;
            }
        }

        /// <summary>
        /// Calls the Load() method on all existing pages to ensure they
        /// are Dark Mode and Light Mode compliant
        /// </summary>
        public void ReloadPreviousPages()
        {
            foreach(var pageKey in PageList)
            {
                Control[] _Pages = Controls.Find(pageKey, true);
                _Pages[0].Refresh(); 
            }
        }

    } // End of MainForm class
}
