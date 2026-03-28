using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SortMyMedia.FaceRecognition
{
    public readonly struct PersonNamingResponse
    {
        public PersonNamingResponse(string name, bool dontAskAgain)
        {
            Name = name;
            DontAskAgain = dontAskAgain;
        }

        public string Name { get; }
        public bool DontAskAgain { get; }
    }

    public interface IPersonInteractionService
    {
        PersonNamingResponse AskForNewPersonName(IReadOnlyList<string> representativeThumbnails, IReadOnlyList<string> nameSuggestions);
        bool ConfirmCandidate(string candidateName, IReadOnlyList<string> representativeThumbnails);
    }

    public sealed class PersonNamingService : IPersonInteractionService
    {
        private readonly ConsolePersonInteractionService consoleFallback = new();

        public PersonNamingResponse AskForNewPersonName(IReadOnlyList<string> representativeThumbnails, IReadOnlyList<string> nameSuggestions)
        {
            if (!CanUseWinForms())
                return consoleFallback.AskForNewPersonName(representativeThumbnails, nameSuggestions);

            using var form = new PersonNamingDialog(representativeThumbnails, nameSuggestions);
            DialogResult result = form.ShowDialog();
            if (result != DialogResult.OK)
                return new PersonNamingResponse(string.Empty, false);

            return new PersonNamingResponse(form.PersonName, form.DontAskAgain);
        }

        public bool ConfirmCandidate(string candidateName, IReadOnlyList<string> representativeThumbnails)
        {
            if (!CanUseWinForms())
                return consoleFallback.ConfirmCandidate(candidateName, representativeThumbnails);

            using var form = new PersonConfirmDialog(candidateName, representativeThumbnails);
            return form.ShowDialog() == DialogResult.Yes;
        }

        private static bool CanUseWinForms()
        {
            try
            {
                return Environment.UserInteractive && Application.MessageLoop;
            }
            catch
            {
                return false;
            }
        }

        private sealed class PersonNamingDialog : Form
        {
            private readonly TextBox nameTextBox;
            private readonly CheckBox dontAskAgainCheckBox;

            public PersonNamingDialog(IReadOnlyList<string> thumbnails, IReadOnlyList<string> nameSuggestions)
            {
                Text = "Name Person";
                StartPosition = FormStartPosition.CenterParent;
                Width = 760;
                Height = 460;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;

                var title = new Label
                {
                    Text = "This appears to be a new person. What is their name?",
                    AutoSize = true,
                    Left = 16,
                    Top = 14
                };
                Controls.Add(title);

                var thumbsPanel = BuildThumbnailPanel(thumbnails);
                thumbsPanel.Left = 16;
                thumbsPanel.Top = 44;
                Controls.Add(thumbsPanel);

                nameTextBox = new TextBox { Left = 16, Top = 282, Width = 520 };
                ConfigureAutoComplete(nameTextBox, nameSuggestions);
                Controls.Add(nameTextBox);

                dontAskAgainCheckBox = new CheckBox
                {
                    Left = 16,
                    Top = 316,
                    Width = 640,
                    Text = "Don't ask again — automatically assign names like Person_1, Person_2, ..."
                };
                Controls.Add(dontAskAgainCheckBox);

                var okButton = new Button { Text = "OK", Left = 560, Top = 360, Width = 80, DialogResult = DialogResult.OK };
                var cancelButton = new Button { Text = "Cancel", Left = 650, Top = 360, Width = 80, DialogResult = DialogResult.Cancel };
                Controls.Add(okButton);
                Controls.Add(cancelButton);

                AcceptButton = okButton;
                CancelButton = cancelButton;
            }

            public string PersonName => nameTextBox.Text?.Trim() ?? string.Empty;
            public bool DontAskAgain => dontAskAgainCheckBox.Checked;

            private static void ConfigureAutoComplete(TextBox textBox, IReadOnlyList<string> suggestions)
            {
                var source = new AutoCompleteStringCollection();
                foreach (string suggestion in suggestions.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase))
                    source.Add(suggestion);

                textBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                textBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
                textBox.AutoCompleteCustomSource = source;
            }
        }

        private sealed class PersonConfirmDialog : Form
        {
            public PersonConfirmDialog(string candidateName, IReadOnlyList<string> thumbnails)
            {
                Text = "Confirm Person";
                StartPosition = FormStartPosition.CenterParent;
                Width = 760;
                Height = 430;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;

                var title = new Label
                {
                    Text = $"Is this {candidateName}?",
                    AutoSize = true,
                    Left = 16,
                    Top = 14
                };
                Controls.Add(title);

                var thumbsPanel = BuildThumbnailPanel(thumbnails);
                thumbsPanel.Left = 16;
                thumbsPanel.Top = 44;
                Controls.Add(thumbsPanel);

                var yesButton = new Button { Text = "Yes", Left = 560, Top = 330, Width = 80, DialogResult = DialogResult.Yes };
                var noButton = new Button { Text = "No", Left = 650, Top = 330, Width = 80, DialogResult = DialogResult.No };
                Controls.Add(yesButton);
                Controls.Add(noButton);

                AcceptButton = yesButton;
                CancelButton = noButton;
            }
        }

        private static FlowLayoutPanel BuildThumbnailPanel(IReadOnlyList<string> thumbnails)
        {
            var panel = new FlowLayoutPanel
            {
                Width = 714,
                Height = 220,
                BorderStyle = BorderStyle.FixedSingle,
                WrapContents = false,
                AutoScroll = true
            };

            foreach (string path in thumbnails.Where(File.Exists).Take(5))
            {
                var pic = new PictureBox
                {
                    Width = 128,
                    Height = 128,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Margin = new Padding(8)
                };

                try
                {
                    pic.Image = Image.FromFile(path);
                }
                catch
                {
                }

                panel.Controls.Add(pic);
            }

            return panel;
        }
    }

    public sealed class ConsolePersonInteractionService : IPersonInteractionService
    {
        public PersonNamingResponse AskForNewPersonName(IReadOnlyList<string> representativeThumbnails, IReadOnlyList<string> nameSuggestions)
        {
            PrintThumbnails(representativeThumbnails);
            if (nameSuggestions.Count > 0)
                Console.WriteLine($"Suggestions: {string.Join(", ", nameSuggestions.Take(8))}");

            Console.WriteLine("This appears to be a new person. What is their name?");
            Console.Write("Name: ");
            string name = Console.ReadLine() ?? string.Empty;

            Console.Write("Don't ask again and auto-assign names? (y/N): ");
            bool dontAskAgain = string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase);

            return new PersonNamingResponse(name, dontAskAgain);
        }

        public bool ConfirmCandidate(string candidateName, IReadOnlyList<string> representativeThumbnails)
        {
            PrintThumbnails(representativeThumbnails);
            Console.Write($"Is this {candidateName}? (y/N): ");
            return string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase);
        }

        private static void PrintThumbnails(IReadOnlyList<string> representativeThumbnails)
        {
            if (representativeThumbnails.Count == 0)
                return;

            Console.WriteLine("Representative thumbnails:");
            foreach (string path in representativeThumbnails.Where(p => !string.IsNullOrWhiteSpace(p)))
                Console.WriteLine($" - {path}");
        }
    }
}
