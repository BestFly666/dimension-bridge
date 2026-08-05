using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SimpleXmlEditor.ExpertProfiles;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor
{
    public partial class SettingsWindow
    {
        #region Expert Profile Management

        private void RefreshProfilesList()
        {
            ProfilesListBox.ItemsSource = null;
            ProfilesListBox.ItemsSource = _profileManager.Profiles;
        }

        private void AddProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            ProfileEditorTitle.Text = $"➕ {LocalizationManager.GetString("NewProfile")}";
            ProfileNameTxt.Text = "";
            ProfileDescTxt.Text = "";
            ProfileContextTxt.Text = "";
            ProfileGlossaryTxt.Text = "";
            ProfileEditorPanel.Visibility = Visibility.Visible;
            ProfileNameTxt.Focus();
        }

        private void EditProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var profile = button?.DataContext as ExpertProfile;
            if (profile != null)
            {
                EditProfile(profile);
            }
        }

        private void ProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfilesListBox.SelectedItem is ExpertProfile profile)
            {
                EditProfile(profile);
            }
        }

        private void EditProfile(ExpertProfile profile)
        {
            ProfileEditorTitle.Text = $"✏️ {LocalizationManager.GetString("EditProfile", profile.Name)}";
            ProfileNameTxt.Text = profile.Name;
            ProfileDescTxt.Text = profile.Description;
            ProfileContextTxt.Text = profile.Context;

            // Convert glossary dictionary to text lines
            var glossaryLines = new System.Text.StringBuilder();
            if (profile.Glossary != null)
            {
                foreach (var kvp in profile.Glossary)
                {
                    glossaryLines.AppendLine($"{kvp.Key} = {kvp.Value}");
                }
            }
            ProfileGlossaryTxt.Text = glossaryLines.ToString().TrimEnd();
            ProfileEditorPanel.Visibility = Visibility.Visible;
        }

        private void DeleteProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var profile = button?.DataContext as ExpertProfile;
            if (profile == null) return;

            var result = MessageBox.Show(
                LocalizationManager.GetString("ConfirmDeleteProfile", profile.Name), 
                LocalizationManager.GetString("MsgConfirm"), 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _profileManager.DeleteProfile(profile.Name);
                if (ActiveExpertProfile == profile.Name)
                    ActiveExpertProfile = "";
                HideProfileEditor();
                RefreshProfilesList();
            }
        }

        private void SaveProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            var name = ProfileNameTxt.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(LocalizationManager.GetString("EnterProfileName"), LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var profile = new ExpertProfile
            {
                Name = name,
                Description = ProfileDescTxt.Text.Trim(),
                Context = ProfileContextTxt.Text.Trim()
            };

            // Parse glossary from text lines
            var glossaryText = ProfileGlossaryTxt.Text.Trim();
            if (!string.IsNullOrEmpty(glossaryText))
            {
                profile.Glossary = new Dictionary<string, string>();
                var lines = glossaryText.Split('\n');
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    var eqIndex = trimmed.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        var term = trimmed.Substring(0, eqIndex).Trim();
                        var translation = trimmed.Substring(eqIndex + 1).Trim();
                        if (!string.IsNullOrEmpty(term) && !string.IsNullOrEmpty(translation))
                        {
                            profile.Glossary[term] = translation;
                        }
                    }
                }
            }

            _profileManager.AddProfile(profile);
            HideProfileEditor();
            RefreshProfilesList();
        }

        private void CancelProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            HideProfileEditor();
        }

        private void HideProfileEditor()
        {
            ProfileEditorPanel.Visibility = Visibility.Collapsed;
            ProfileNameTxt.Text = "";
            ProfileDescTxt.Text = "";
            ProfileContextTxt.Text = "";
            ProfileGlossaryTxt.Text = "";
            ProfilesListBox.SelectedItem = null;
        }

        #endregion
    }
}
