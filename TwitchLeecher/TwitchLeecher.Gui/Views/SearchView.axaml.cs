using System;
using Avalonia.Controls;
using Avalonia.Input;
using TwitchLeecher.Gui.ViewModels;

namespace TwitchLeecher.Gui.Views
{
    public partial class SearchView : UserControl
    {
        public SearchView()
        {
            InitializeComponent();

            // Future days stay in the calendar, dimmed, but cannot be chosen: a blank end of the month looked
            // like dead space. (Blackout dates, rather than DisplayDateEnd, which hides them.)
            // The picker only creates its blackout list when its template is applied, so wait for that.
            foreach (CalendarDatePicker picker in new[] { FromPicker, ToPicker })
            {
                picker.TemplateApplied += (_, _) =>
                {
                    if (picker.BlackoutDates != null && picker.BlackoutDates.Count == 0)
                    {
                        picker.BlackoutDates.Add(new CalendarDateRange(DateTime.Today.AddDays(1), DateTime.Today.AddYears(5)));
                    }
                };
            }
        }
    }
}