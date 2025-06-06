﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// jogeurte 11/19

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using WDAC_Wizard.Properties;

namespace WDAC_Wizard
{
    public partial class ConfigTemplate_Control : UserControl
    {        
        public MainWindow _MainWindow;
        private WDAC_Policy Policy;

        public ConfigTemplate_Control(MainWindow pMainWindow)
        {
            InitializeComponent();
            Policy = pMainWindow.Policy; 
            _MainWindow = pMainWindow;
            _MainWindow.ErrorOnPage = false;
            _MainWindow.RedoFlowRequired = false; // Nothing on this page will change the state of this
            Logger.Log.AddInfoMsg("==== Configuration Template Page Initialized ====");
        }

        /// <summary>
        /// Method is executed when the user control is loaded. Sets the default values for the
        /// buttons depending on the template policy, or the edited policy. 
        /// </summary>
        private void SetDefaultButtonVals(object sender, EventArgs e)
        {
            // **** This function is run on UI load  *** //

            Policy._PolicyType = _MainWindow.Policy._PolicyType; 
            Policy._PolicyTemplate = _MainWindow.Policy._PolicyTemplate; 
            Policy.EditPolicyPath = _MainWindow.Policy.EditPolicyPath;

            Policy.ConfigRules = InitRulesDict(); // If supplemental, need to disable the button

            // If unable to read the CI policy, fail gracefully and return to the home page
            if (!ReadSetRules(sender, e))
                return; 

            // Set HVCI option value
            if (Policy.EnableHVCI)
            {
                Policy.ConfigRules["HVCI"]["CurrentValue"] = Policy.ConfigRules["HVCI"]["AllowedValue"];
            }

            Dictionary<string, Dictionary<string, string>>.KeyCollection keys = Policy.ConfigRules.Keys;
            foreach (string key in keys)
            {
                // If unsupported, skip
                if (!Convert.ToBoolean(Policy.ConfigRules[key]["Supported"]))
                {
                    continue;
                }

                // Get the button (UI element) name to modify the state of the button
                string buttonName = Policy.ConfigRules[key]["ButtonMapping"];
                string labelName = "label_" + buttonName.Substring(buttonName.IndexOf('_') + 1); 

                // If the policy rule current value matches the allowed value, rule has been set
                if (Policy.ConfigRules[key]["CurrentValue"] == Policy.ConfigRules[key]["AllowedValue"]) 
                {
                    // Set button to toggled mode
                    Controls.Find(buttonName, true).FirstOrDefault().Tag = "toggle";
                    Controls.Find(buttonName, true).FirstOrDefault().BackgroundImage = Resources.toggle;
                }
                else
                {
                    // Set button to untoggled mode
                    Controls.Find(buttonName, true).FirstOrDefault().Tag = "untoggle";
                    Controls.Find(buttonName, true).FirstOrDefault().BackgroundImage = Resources.untoggle_old;
                }

                // Depending on the policy, e.g. supplementals, do not allow user to modify the state of some rule-options
                if (Policy._PolicyType == WDAC_Policy.PolicyType.SupplementalPolicy
                    || Policy.siPolicy.PolicyType == global::PolicyType.SupplementalPolicy)
                { 
                    switch(Policy.ConfigRules[key]["ValidSupplemental"]){
                    case "True":
                        Controls.Find(buttonName, true).FirstOrDefault().Enabled = true;
                        Controls.Find(labelName, true).FirstOrDefault().Tag = "Enabled";
                        Controls.Find(labelName, true).FirstOrDefault().ForeColor = Color.Black;
                        break;

                    case "False":
                        Controls.Find(buttonName, true).FirstOrDefault().Enabled = false;
                        Controls.Find(labelName, true).FirstOrDefault().Tag = "Grayed";
                        Controls.Find(labelName, true).FirstOrDefault().ForeColor = Color.Gray;
                        break;

                    case "False-NoInherit":
                        Controls.Find(buttonName, true).FirstOrDefault().Enabled = false;
                        Controls.Find(labelName, true).FirstOrDefault().Tag = "Grayed-NoInherit";
                        Controls.Find(labelName, true).FirstOrDefault().ForeColor = Color.Gray;
                        break; 
                    }
                }

                else if(Policy._PolicyType == WDAC_Policy.PolicyType.AppIdTaggingPolicy)
                {
                    switch (Policy.ConfigRules[key]["ValidAppId"])
                    {
                        case "True":
                            Controls.Find(buttonName, true).FirstOrDefault().Enabled = true;
                            Controls.Find(labelName, true).FirstOrDefault().Tag = "Enabled";
                            Controls.Find(labelName, true).FirstOrDefault().ForeColor = Color.Black;
                            break;

                        case "False":
                        case "False-Inherit":
                            Controls.Find(buttonName, true).FirstOrDefault().Enabled = false;
                            Controls.Find(labelName, true).FirstOrDefault().Tag = "Invalid-AppId";
                            Controls.Find(labelName, true).FirstOrDefault().ForeColor = Color.Gray;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Sets the state of the rule-option button and its state in the Policy object
        /// </summary>
        /// <param name="buttonName">String containing the name of the toggle button
        /// build operations</param>
        private void SetButtonVal(string buttonName)
        {
            // Have to cycle through all of the keys to find the button name value
            Dictionary<string, Dictionary<string, string>>.KeyCollection keys = Policy.ConfigRules.Keys;
            foreach (string key in keys)
            {
                // Found button name and rule-option mapping
                if (Policy.ConfigRules[key]["ButtonMapping"] == buttonName) // button name match
                {
                    if (Controls.Find(buttonName, true).FirstOrDefault().Tag.ToString() == "untoggle")
                    {
                        // Set button to toggled mode
                        Controls.Find(buttonName, true).FirstOrDefault().Tag = "toggle";

                        // Dark Mode
                        if(Properties.Settings.Default.useDarkMode)
                        {
                            Controls.Find(buttonName, true).FirstOrDefault().BackgroundImage = Resources.darkmode_toggle;
                        }

                        // Light Mode
                        else
                        {
                            Controls.Find(buttonName, true).FirstOrDefault().BackgroundImage = Resources.toggle;
                        }
                        
                        Policy.ConfigRules[key]["CurrentValue"] = Policy.ConfigRules[key]["AllowedValue"];
                        SetRuleOptionState(key); 
                    }
                    else
                    {
                        // Set button to untoggled mode
                        Controls.Find(buttonName, true).FirstOrDefault().Tag = "untoggle";
                        Controls.Find(buttonName, true).FirstOrDefault().BackgroundImage = Resources.untoggle_old;

                        Policy.ConfigRules[key]["CurrentValue"] = GetOppositeOption(Policy.ConfigRules[key]["AllowedValue"]);
                        SetRuleOptionState(key, true);
                    }

                    Logger.Log.AddInfoMsg(String.Format("Rule-Option Setting Changed --- {0}: {1}", key, Policy.ConfigRules[key]["CurrentValue"]));
                    break; // break out of foreach, we found the button
                }
            } 
        }

        /// <summary>
        /// All of the mouse hovers over the rule-option labels trigger this method to set the 
        /// option description on the UI. 
        /// </summary>
        private void RuleLabel_Hover(object sender, EventArgs e)
        {
            label_Info.Visible = true;
            string _type = sender.GetType().Name;

            // If hovering on the user control (i.e. not a label, remove label Info text
            // All of the descriptions are stored in  Resources.resx
            if (_type == "ConfigTemplate_Control")
            {
                label_Info.Text = "";
                return;
            }

            // Break early for invalid or invalid-inherited rule-options
            if (((Label)sender).Tag.ToString() == "Grayed")
            {
                label_Info.Text = Resources.InvalidSupplementalRule_Info;
                return;
            }

            else if (((Label)sender).Tag.ToString() == "Grayed-NoInherit")
            {
                label_Info.Text = Resources.InvalidSupplementalRule_NoInherit_Info;
                return;
            }

            else if (((Label)sender).Tag.ToString() == "Invalid-AppId")
            {
                label_Info.Text = Resources.InvalidAppIdRuleOption_Info;
                return;
            }

            // Valid Rule - Show rule-option description
            switch (((Label)sender).Text){
            case "User Mode Code Integrity":
                label_Info.Text = Resources.UMCI_Info;
                break;

            case "Boot Menu Protection":
                label_Info.Text = Resources.UnsupportedRule_Info;
                break;

            case "Require WHQL":
                label_Info.Text = Resources.WHQL_Info;
                break;

            case "Inherit Default Policy":
                label_Info.Text = Resources.UnsupportedRule_Info;
                break;

            case "Unsigned System Integrity Policy":
                label_Info.Text = Resources.UnsignedPolicy_Info;
                break;

            case "Advanced Boot Options Menu":
                label_Info.Text = Resources.AdvancedBootOpts_Info;
                break;

            case "Disable Script Enforcement":
                label_Info.Text = Resources.ScriptEnforcement_Info;
                break;

            case "Enforce Store Applications":
                label_Info.Text = Resources.StoreApps_Info;
                break;

            case "Managed Installer":
                label_Info.Text = Resources.ManagedInst_Info;
                break;

            case "Intelligent Security Graph":
                label_Info.Text = Resources.ISG_Info;
                break;

            case "Update Policy without Rebooting":
                label_Info.Text = Resources.NoReboot_Info;
                break;

            case "Allow Supplemental Policies":
                label_Info.Text = Resources.SuppPolicies_Info;
                break;

            case "Hypervisor-protected Code Integrity    ":
                label_Info.Text = Resources.HVCI_Info;
                break;

            case "Disable Flight Signing":
                label_Info.Text = Resources.FlightSigning_Info;
                break;

            case "Allow Debug Policy Augmented":
                label_Info.Text = Resources.UnsupportedRule_Info;
                break;

            case "Require EV Signers":
                label_Info.Text = Resources.EVSigners_Info;
                break;

            case "Boot Audit on Failure":
                label_Info.Text = Resources.BootAudit_Info;
                break;

            case "Invalidate EAs on Reboot":
                label_Info.Text = Resources.InvalidateEAs_Info;
                break;

            case "Disable Runtime Filepath Rules":
                label_Info.Text = Resources.RuntimeRules;
                break;

            case "Dynamic Code Security":
                label_Info.Text = Resources.DynamicSecurity_Info;
                break;

            case "Treat Revoked as Unsigned":
                label_Info.Text = Resources.RevokedAsUnsigned_Info;
                break; 

            default:
                label_Info.Text = "";
                break; 
            }

            // Format the label to fit at the bottom of the page.
            // Set the cut location at the 85th percentile space location. 
            if(label_Info.Text.Length > 135)
            {
                string _tmp = label_Info.Text;
                var idx = new List<int>();
                for (int i = _tmp.IndexOf(' '); i > -1; i = _tmp.IndexOf(' ', i + 1))
                    idx.Add(i);
                int cutLoc = Convert.ToInt32(Math.Round(idx.Count * 0.85)); 
                label_Info.Text = _tmp.Substring(0,idx[cutLoc]) + "\r\n" + _tmp.Substring(idx[cutLoc]+1);
            }
            
        }

        /// <summary>
        /// Makes visible the advanced options panel. 
        /// </summary>
        private void AdvancedOptions_ButtonClick(object sender, EventArgs e)
        {
            // Flip the state of the advanced options panel and the text for the button label
            if (!panel_AdvancedOptions.Visible)
            {
                panel_AdvancedOptions.Visible = true;
                label_AdvancedOptions.Text = "- Advanced Options"; 
            }
            else
            {
                panel_AdvancedOptions.Visible = false;
                label_AdvancedOptions.Text = "+ Advanced Options"; 
            }
            Logger.Log.AddInfoMsg("Advanced options clicked.");
        }

        /// <summary>
        /// Parses the RulesDict xml document and extracts the AllowedValue, corresponding button name
        /// whether or not the rule is supported, and the option number. 
        /// </summary>
        /// <returns>Config rules dictionary which the fields from the RulesDict xml file</returns>
        private Dictionary<string, Dictionary<string, string>> InitRulesDict()
        {
            // Wiring the button IDs to the corresponding rules
            // Default values and order can be found @: https://docs.microsoft.com/windows/security/threat-protection/windows-defender-application-control/select-types-of-rules-to-create#windows-defender-application-control-policy-rules
            string dictPath = Path.Combine(_MainWindow.ExeFolderPath,"RulesDict.xml");
            Dictionary<string, Dictionary<string, string>> rulesDict = new Dictionary<string, Dictionary<string, string>>();

            try
            {
                XmlTextReader xmlReader = new XmlTextReader(dictPath);
                while (xmlReader.Read())
                {
                    if(xmlReader.Name == "Rules")
                    {
                        while(xmlReader.Read())
                        {
                            if(xmlReader.NodeType == XmlNodeType.Element)
                            {
                                Dictionary<string, string> tempDict = new Dictionary<string, string>();
                                string ruleName = xmlReader.GetAttribute("Name");
                                tempDict.Add("AllowedValue", xmlReader.GetAttribute("AllowedValue"));
                                tempDict.Add("ButtonMapping", xmlReader.GetAttribute("ButtonMapping"));
                                tempDict.Add("CurrentValue", GetOppositeOption(tempDict["AllowedValue"]));
                                tempDict.Add("Supported", xmlReader.GetAttribute("Supported"));
                                tempDict.Add("RuleNumber", xmlReader.GetAttribute("RuleNumber"));
                                tempDict.Add("ValidSupplemental", xmlReader.GetAttribute("ValidSupplemental"));
                                tempDict.Add("ValidAppId", xmlReader.GetAttribute("ValidAppIdTagging"));
                                rulesDict.Add(ruleName, tempDict);
                            }
                        }
                    }         
                }
            }
            catch(Exception e)
            {
                Logger.Log.AddErrorMsg("Reading RulesDict.xml in InitRulesDict() encountered the following error: ", e); 
            }
            return rulesDict; 
        }

        /// <summary>
        /// All of the button trigger events trigger this method. Casts the sender to get the button name and calls the SetButtonVal using the
        /// triggered button name. 
        /// </summary>
        private void Toggle_Button_Click(object sender, EventArgs e)
        {
            // Get button name from sender and set UI state
            string buttonName = ((Button)sender).Name;
            SetButtonVal(buttonName);

            // Update the policy Rule-Options object
            _MainWindow.Policy.ConfigRules = Policy.ConfigRules;
            Policy.EnableHVCI = Policy.ConfigRules["HVCI"]["CurrentValue"] == "Enabled";
            _MainWindow.Policy.EnableHVCI = Policy.EnableHVCI; 
        }

        /// <summary>
        /// Maps the rule-option name to OptionType
        /// </summary>
        /// <param name="key"></param>
        /// <param name="removeOption"></param>
        private void SetRuleOptionState(string key, bool removeOption=false)
        {
            RuleType ruleOption = new RuleType(); 
            switch (key){
            case "UMCI":
                ruleOption.Item = OptionType.EnabledUMCI;
                break;

            case "BootMenuProtection":
                ruleOption.Item = OptionType.EnabledBootMenuProtection;
                break;

            case "WHQL":
                ruleOption.Item = OptionType.RequiredWHQL;
                break;

            case "AuditMode":
                ruleOption.Item = OptionType.EnabledAuditMode;
                break;

            case "InheritDefaultPolicy":
                ruleOption.Item = OptionType.EnabledInheritDefaultPolicy;
                break;

            case "UnsignedSystemIntegrityPolicy":
                ruleOption.Item = OptionType.EnabledUnsignedSystemIntegrityPolicy;
                break;

            case "AdvancedBootOptionsMenu":
                ruleOption.Item = OptionType.EnabledAdvancedBootOptionsMenu;
                break;

            case "ScriptEnforcement":
                ruleOption.Item = OptionType.DisabledScriptEnforcement;
                break;

            case "EnforceStoreApplications":
                ruleOption.Item = OptionType.RequiredEnforceStoreApplications;
                break;

            case "ManagedInstaller":
                ruleOption.Item = OptionType.EnabledManagedInstaller;
                break;

            case "IntelligentSecurityGraphAuthorization":
                ruleOption.Item = OptionType.EnabledIntelligentSecurityGraphAuthorization;
                break;

            case "UpdatePolicyNoReboot":
                ruleOption.Item = OptionType.EnabledUpdatePolicyNoReboot;
                break;

            case "AllowSupplementalPolicies":
                ruleOption.Item = OptionType.EnabledAllowSupplementalPolicies;
                break;

            case "FlightSigning":
                ruleOption.Item = OptionType.DisabledFlightSigning;
                break;

            case "Allow Debug Policy Augmented":
                //ruleOption.Item = OptionType.;
                break;

            case "EVSigners":
                ruleOption.Item = OptionType.RequiredEVSigners;
                break;

            case "BootAuditOnFailure":
                ruleOption.Item = OptionType.EnabledBootAuditOnFailure;
                break;

            case "InvalidateEAsonReboot":
                ruleOption.Item = OptionType.EnabledInvalidateEAsonReboot;
                break;

            case "RuntimeFilePathRuleProtection":
                ruleOption.Item = OptionType.DisabledRuntimeFilePathRuleProtection;
                break;

            case "DynamicCodeSecurity":
                ruleOption.Item = OptionType.EnabledDynamicCodeSecurity;
                break;

            case "RevokedExpiredAsUnsigned":
                ruleOption.Item = OptionType.EnabledRevokedExpiredAsUnsigned;
                break;

            default:
                return;
            }

            if(removeOption)
            {
                for(int i=0; i < _MainWindow.Policy.PolicyRuleOptions.Count; i++)
                {
                    if(_MainWindow.Policy.PolicyRuleOptions[i].Item == ruleOption.Item)
                    {
                        _MainWindow.Policy.PolicyRuleOptions.RemoveAt(i);
                        return;
                    }
                }
            }
            else
            {
                _MainWindow.Policy.PolicyRuleOptions.Add(ruleOption);
            }
        }

        /// <summary>
        /// Parses the template or the policy that the user would like to edit. Sets all of the 
        /// CI Policy related fields from the xml document. 
        /// </summary>
        private bool ReadSetRules(object sender, EventArgs e)
        {
            // Read in the pre-set policy rules and HVCI option from either:
            // Template policy schema file IF NEW policy selected
            // Pre-existing base policy IF EDIT policy selected

            string xmlPathToRead = GetTemplatePath();
            Logger.Log.AddInfoMsg(String.Format("--- Reading Set Rules from {0} ---", xmlPathToRead));

            // Read File
            SiPolicy sipolicy = Helper.DeserializeXMLtoPolicy(xmlPathToRead); 
            if(sipolicy == null)
            {
                Logger.Log.AddErrorMsg("Reading the xml CI policy failed during ReadSetRules");
                
                // Prompt user for additional confirmation
                DialogResult res = MessageBox.Show("The Wizard is unable to read your CI policy xml file. The policy XML may be corrupted. ",
                                                    "Parsing Error",
                                                    MessageBoxButtons.OK,
                                                    MessageBoxIcon.Error);

                if (res == DialogResult.OK)
                {
                    _MainWindow.ResetWorkflow(sender, e);
                }
                   
                return false;
            }

            Policy.siPolicy = sipolicy;

            // Set the UI state for the rule-options
            DisplayRulesUI();

            Policy.EnableHVCI = Policy.siPolicy.HvciOptions > 0;
            _MainWindow.Policy.ConfigRules = Policy.ConfigRules;
            _MainWindow.Policy.EnableHVCI = Policy.EnableHVCI;
            _MainWindow.Policy.siPolicy = Policy.siPolicy;

            // Copy template to temp folder for reading and writing unless template already in temp folder (event log conversion)
            if (!xmlPathToRead.Contains(_MainWindow.TempFolderPath))
            {
                string xmlTemplateToWrite = Path.Combine(_MainWindow.TempFolderPath, Path.GetFileName(xmlPathToRead));
                File.Copy(xmlPathToRead, xmlTemplateToWrite, true);
                _MainWindow.Policy.TemplatePath = xmlTemplateToWrite;
            }
            else
            {
                _MainWindow.Policy.TemplatePath = xmlPathToRead;
            }

            // Set TemplatePath to none for NEW Supplemental policy flow
            if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.New
                && Policy._PolicyType == WDAC_Policy.PolicyType.SupplementalPolicy)
            {
                _MainWindow.Policy.TemplatePath = null;
            }


            return true; 
        }

        /// <summary>
        /// Sets the slider UI state (enabled/disabled) for each rule option 
        /// </summary>
        private void DisplayRulesUI()
        {
            // Merge with configRules:
            foreach (var rule in Policy.siPolicy.Rules)
            {
                string value = ParseRule(rule.Item.ToString())[0];
                string name = ParseRule(rule.Item.ToString())[1];

                if (Policy.ConfigRules.ContainsKey(name))
                {
                    if (Policy._PolicyType == WDAC_Policy.PolicyType.SupplementalPolicy)
                    {
                        // If valid supplemental, add to PolicyRuleOptions struct to be set during policy build
                        if (Policy.ConfigRules[name]["ValidSupplemental"] == "True")
                        {
                            _MainWindow.Policy.PolicyRuleOptions.Add(rule);
                        }

                        // If the policy rule is not a valid supplemental option AND should not be inherited from base, e.g. AllowSupplementals
                        // Set the value to not enabled (Get Opposite Value)
                        if (Policy.ConfigRules[name]["ValidSupplemental"] == "False-NoInherit")
                        {
                            Policy.ConfigRules[name]["CurrentValue"] = GetOppositeOption(value);
                        }
                        else
                        {
                            Policy.ConfigRules[name]["CurrentValue"] = value;
                        }

                        // Mirror the value for signed policy based on the value in the base policy
                        // This will allow for the resulting xml to compile to bin
                        if (name == "UnsignedSystemIntegrityPolicy")
                        {
                            SetRuleOptionState(name);
                        }

                    }
                    else if (Policy._PolicyType == WDAC_Policy.PolicyType.AppIdTaggingPolicy)
                    {
                        // If valid AppIdTagging Policy, add to PolicyRuleOptions struct to be set during policy build
                        if (Policy.ConfigRules[name]["ValidAppId"] != "False")
                        {
                            Policy.ConfigRules[name]["CurrentValue"] = value;
                            _MainWindow.Policy.PolicyRuleOptions.Add(rule);
                        }
                        else
                        {
                            Policy.ConfigRules[name]["CurrentValue"] = GetOppositeOption(value);
                        }
                    }
                    else
                    {
                        Policy.ConfigRules[name]["CurrentValue"] = value;
                        _MainWindow.Policy.PolicyRuleOptions.Add(rule);
                    }
                }
            }
        }

        /// <summary>
        /// Looks up the policy xml path to parse the rule-options
        /// </summary>
        /// <returns></returns>
        private string GetTemplatePath()
        {
            string xmlPathToRead = String.Empty;

            // If we are editing a policy, read the EditPolicyPath
            // We need to know whether we are editing a base or supplemental policy
            if (Policy.PolicyWorkflow == WDAC_Policy.Workflow.Edit)
            {
                xmlPathToRead = _MainWindow.Policy.EditPolicyPath;
            }

            // If we are supplementing a policy, we need to mirror the rule options of the base so they do not conflict
            else if (Policy._PolicyType == WDAC_Policy.PolicyType.SupplementalPolicy)
            {
                // User only provided a base policy ID to expand
                if (_MainWindow.Policy.BasePolicyId != Guid.Empty)
                {
                    xmlPathToRead = Path.Combine(_MainWindow.ExeFolderPath,
                                                            "Templates", Resources.EmptyWdacSupplementalXml);
                }
                // User provided a path to the base policy
                else
                {
                    xmlPathToRead = _MainWindow.Policy.BaseToSupplementPath;
                }
            }

            // Base Policy - Check for policy format and template
            else if (Policy._PolicyType == WDAC_Policy.PolicyType.BasePolicy)
            {
                if (Policy._Format == WDAC_Policy.Format.MultiPolicy)
                {
                    // Multi-policy Format Policy Templates
                    switch (Policy._PolicyTemplate)
                    {
                        case WDAC_Policy.NewPolicyTemplate.WindowsWorks:
                            // Windows Works Mode 
                            xmlPathToRead = Path.Combine(_MainWindow.ExeFolderPath, "Templates", Resources.WindowsTemplate);
                            break;

                        case WDAC_Policy.NewPolicyTemplate.SignedReputable:
                            // Signed and Reputable Mode
                            xmlPathToRead = Path.Combine(_MainWindow.ExeFolderPath, "Templates", Resources.SACTemplate);
                            break;

                        case WDAC_Policy.NewPolicyTemplate.AllowMicrosoft:
                            // Allow Microsoft mode
                            xmlPathToRead = Path.Combine(_MainWindow.ExeFolderPath, "Templates", Resources.MicrosoftTemplate);
                            break;
                    }
                }
                else
                {
                    // Legacy/single-policy Format Policy Templates
                    switch (Policy._PolicyTemplate)
                    {
                        case WDAC_Policy.NewPolicyTemplate.WindowsWorks:
                            // Windows Works Mode 
                            xmlPathToRead = Path.Combine(_MainWindow.ExeFolderPath, "Templates", Resources.WindowsSingleTemplate);
                            break;

                        case WDAC_Policy.NewPolicyTemplate.SignedReputable:
                            // Signed and Reputable Mode
                            xmlPathToRead = Path.Combine(_MainWindow.ExeFolderPath, "Templates", Resources.SACSingleTemplate);
                            break;

                        case WDAC_Policy.NewPolicyTemplate.AllowMicrosoft:
                            // Allow Microsoft mode
                            xmlPathToRead = Path.Combine(_MainWindow.ExeFolderPath, "Templates", Resources.MicrosoftSingleTemplate);
                            break;
                    }
                }
            }

            // AppIdTagging Policy
            else if (Policy._PolicyType == WDAC_Policy.PolicyType.AppIdTaggingPolicy)
            {
                xmlPathToRead = Path.Combine(_MainWindow.ExeFolderPath, "Templates", Resources.AppIdTaggingTemplate);
            }

            return xmlPathToRead;
        }

        /// <summary>
        /// Takes in the allowed value such as Enabled, Disabled, Allowed, Required, and returns the opposite value
        /// such that the option will not be set in the cmdlet. 
        /// </summary>
        /// <param name="allowedOption">Allowed values to set the options to in the Set-RuleOption cmdlet</param>
        /// <returns>String opposite of the allowed string values. Eg) Enabled returns Disabled</returns>
        private string GetOppositeOption(string allowedOption)
        {
            string oppOption = "";

            switch(allowedOption){
            case "Enabled":
                oppOption =  "Disabled";
                break;

            case "Disabled":
                oppOption =  "Enabled";
                break;

            case "Required":
                oppOption =  "Disabled";
                break;

            case "Allowed":
                oppOption = "Disabled";
                break; 
            }

            return oppOption; 
        }

        private List<string> ParseRule(string rule)
        {
            List<string> parsedRule = new List<string>(); 
            if (rule.Contains("Enabled"))
            {
                int eop = 7;
                parsedRule.Add(rule.Substring(0, eop)); 
                parsedRule.Add(rule.Substring(eop));
            }

            else if (rule.Contains("Disabled"))
            {
                int eop = 8;
                parsedRule.Add(rule.Substring(0, eop));
                parsedRule.Add(rule.Substring(eop));
            }

            else if (rule.Contains("Allowed"))
            {
                int eop = 7;
                parsedRule.Add(rule.Substring(0, eop));
                parsedRule.Add(rule.Substring(eop));
            }

            else if (rule.Contains("Required"))
            {
                int eop = 8;
                parsedRule.Add(rule.Substring(0, eop));
                parsedRule.Add(rule.Substring(eop));
            }

            else
            {
                Logger.Log.AddErrorMsg("Rule Value not found for rule " + rule);
            }
            return parsedRule; 
        }

        /// <summary>
        /// Method to display the audit mode info text. 
        /// </summary>
        private void Display_Audit_Recommendation(object sender, EventArgs e)
        {
            label_Info.Text = "It is recommended to run new policies in audit mode before enforcement to determine the impacts of the policy.";
            label_Info.Visible = true; 
        }

        private void HVCILabel_Click(object sender, EventArgs e)
        {
            // Label for learn more about policy options clicked. Launch msft docs page. 
            try
            {
                string webpage = "https://docs.microsoft.com/windows/security/threat-protection/device-guard/enable-virtualization-based-protection-of-code-integrity";
                Process.Start(new ProcessStartInfo(webpage) { UseShellExecute = true });
            }
            catch (Exception exp)
            {
                Logger.Log.AddErrorMsg("Launching webpage for policy options link encountered the following error", exp);
            }
        }

        private void LabelPolicyOptions_Click(object sender, EventArgs e)
        {
            // Label for learn more about policy options clicked. Launch msft docs page. 
            try
            {
                string webpage = "https://docs.microsoft.com/windows/security/threat-protection/" +
                    "windows-defender-application-control/select-types-of-rules-to-create";
                Process.Start(new ProcessStartInfo(webpage) { UseShellExecute = true });
            }
            catch (Exception exp)
            {
                Logger.Log.AddErrorMsg("Launching webpage for policy options link encountered the following error", exp);
            }
        }

        /// <summary>
        /// Sets the color of the AdvancedOptions label while 
        /// user is hovering over it
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AdvancedOptions_MouseHover(object sender, EventArgs e)
        {
            Label advOptLabel = ((Label)sender);
            advOptLabel.BackColor = Color.FromArgb(190, 230, 253);
        }

        /// <summary>
        /// Resets the color of the AdvancedOptions label after 
        /// user finishes hovering over it
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AdvancedOptions_MouseLeave(object sender, EventArgs e)
        {
            Label advOptLabel = ((Label)sender);
            advOptLabel.BackColor = Color.Transparent; 
        }

        /// <summary>
        /// Form painting. Occurs on Form.Refresh, Load and Focus. 
        /// Used for UI element changes for Dark and Light Mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConfigTemplate_Control_Validated(object sender, EventArgs e)
        {
            // Set Controls Color (e.g. Panels)
            SetControlsColor();

            // Set Labels Color
            List<Label> labels = new List<Label>();
            GetLabelsRecursive(this, labels);
            SetLabelsColor(labels);

            // Set PolicyType Form back color
            SetFormBackColor();

            // Set toggle colors
            List<Button> toggles = new List<Button>();
            GetTogglesRecursive(this, toggles);
            SetToggleColors(toggles);
        }

        /// <summary>
        /// Gets all of the labels on the form recursively
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="labels"></param>
        private void GetLabelsRecursive(Control parent, List<Label> labels)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Label label)
                {
                    labels.Add(label);
                }
                else
                {
                    GetLabelsRecursive(control, labels);
                }
            }
        }

        /// <summary>
        /// Sets the color of the controls
        /// </summary>
        /// <param name="labels"></param>
        private void SetControlsColor()
        {
            // Dark Mode
            if (Properties.Settings.Default.useDarkMode)
            {
                foreach (Control control in Controls)
                {
                    if (control is Panel panel
                        && (panel.Tag == null || panel.Tag.ToString() != Resources.IgnoreDarkModeTag))
                    {
                        panel.ForeColor = Color.White;
                        panel.BackColor = Color.FromArgb(15,15,15);
                    }
                }
            }

            // Light Mode
            else
            {
                foreach (Control control in Controls)
                {
                    if (control is Panel panel
                        && (panel.Tag == null || panel.Tag.ToString() != Resources.IgnoreDarkModeTag))
                    {
                        panel.ForeColor = Color.Black;
                        panel.BackColor = Color.White;
                    }
                }
            }
        }

        /// <summary>
        /// Sets the color of the labels defined in the provided List
        /// </summary>
        /// <param name="labels"></param>
        private void SetLabelsColor(List<Label> labels)
        {
            // Dark Mode
            if (Properties.Settings.Default.useDarkMode)
            {
                foreach (Label label in labels)
                {
                    if (label.Tag == null || label.Tag.ToString() != Resources.IgnoreDarkModeTag)
                    {
                        label.ForeColor = Color.White;
                        label.BackColor = Color.FromArgb(15,15,15);
                    }
                }
            }

            // Light Mode
            else
            {
                foreach (Label label in labels)
                {
                    if (label.Tag == null || label.Tag.ToString() != Resources.IgnoreDarkModeTag)
                    {
                        label.ForeColor = Color.Black;
                        label.BackColor = Color.White;
                    }
                }
            }

            // Explicitly set color for HVCI label since tag cannot be set to Ignore.. 
            // on this page as it's used for enabled/disabled state
            label_HVCI.ForeColor = Color.FromArgb(16,110,190);
        }

        /// <summary>
        /// Sets the Back Color of the form depending on the
        /// state of Dark and Light Mode
        /// </summary>
        private void SetFormBackColor()
        {
            // Dark Mode
            if (Properties.Settings.Default.useDarkMode)
            {
                BackColor = Color.FromArgb(15, 15, 15);
            }

            // Light Mode
            else
            {
                BackColor = Color.White;
            }
        }

        /// <summary>
        /// Gets all of the toggle buttons on the form recursively
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="labels"></param>
        private void GetTogglesRecursive(Control parent, List<Button> toggles)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Button toggle)
                {
                    toggles.Add(toggle);
                }
                else
                {
                    GetTogglesRecursive(control, toggles);
                }
            }
        }

        /// <summary>
        /// Set all the enabled toggles to light or dark blue
        /// depending on the state of Light and Dark Mode
        /// </summary>
        private void SetToggleColors(List<Button> toggles)
        {
            // Set all Toggle buttons to the correct Light and Dark
            // Mode control colors

            // Dark Mode
            if(Properties.Settings.Default.useDarkMode)
            {
                foreach (Button toggle in toggles)
                {
                    if (toggle.Tag != null)
                    {
                        if (toggle.Tag.ToString() == "toggle")
                        {
                            toggle.BackgroundImage = Resources.darkmode_toggle;
                        }
                    }
                    toggle.BackColor = Color.FromArgb(15, 15, 15); 
                }
            }

            // Light Mode
            else
            {
                foreach (Button toggle in toggles)
                {
                    if (toggle.Tag != null)
                    {
                        if (toggle.Tag.ToString() == "toggle")
                        {
                            toggle.BackgroundImage = Resources.toggle;
                        }
                    }
                    toggle.BackColor = Color.White; 
                }
            }
        }
    }
}
