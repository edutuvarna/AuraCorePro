using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using AuraCore.UI.Avalonia.Services.AI;
using AuraCore.UI.Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace AuraCore.UI.Avalonia.Views.Dialogs;

public partial class ChatOptInDialog : Window
{
    private ModelManagerDialog? _managerDialog;

    public ChatOptInDialog()
    {
        InitializeComponent();
        ApplyLocalization();
        LocalizationService.LanguageChanged += () =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyLocalization);
    }

    public ChatOptInDialog(ChatOptInDialogViewModel vm) : this()
    {
        DataContext = vm;
        vm.RequestClose = accepted => Close(accepted);

        // If user previously acknowledged, we start at Step 2 — mount immediately.
        if (vm.IsStep2) MountStep2();

        vm.PropertyChanged += OnVMPropertyChanged;
    }

    private void OnVMPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatOptInDialogViewModel.CurrentStep)
            && DataContext is ChatOptInDialogViewModel vm
            && vm.IsStep2
            && _managerDialog is null)
        {
            MountStep2();
        }
    }

    private void MountStep2()
    {
        try
        {
            var catalog = App.Services.GetRequiredService<IModelCatalog>();
            var installed = App.Services.GetRequiredService<IInstalledModelStore>();
            var downloader = App.Services.GetRequiredService<IModelDownloadService>();

            var managerVm = new ModelManagerDialogViewModel(catalog, installed, ModelManagerDialogMode.OptIn, downloader);
            managerVm.RequestClose = selected =>
            {
                if (DataContext is ChatOptInDialogViewModel outer)
                {
                    if (selected is not null)
                        outer.CompleteFromStep2(selected.Id);
                    else
                        outer.CancelFromStep2.Execute(null);
                }
            };

            // Populate hero card data from catalog (before recommended model selection by user).
            if (DataContext is ChatOptInDialogViewModel outerVm)
            {
                var recommendedItem = managerVm.Models.FirstOrDefault(m => m.IsRecommended)
                                     ?? managerVm.Models.FirstOrDefault();
                if (recommendedItem is not null)
                {
                    outerVm.RecommendedId = recommendedItem.Model.Id;
                    outerVm.RecommendedDisplayName = recommendedItem.Model.DisplayName;
                }
            }

            _managerDialog = new ModelManagerDialog(managerVm);
            var host = this.FindControl<ContentControl>("PART_Step2Host");
            if (host is not null) host.Content = _managerDialog;
        }
        catch
        {
            // DI not available in design-time / test — ignore
        }
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.Get("chatOptIn.step1.title");

        var step1Label = this.FindControl<TextBlock>("Step1Label");
        if (step1Label is not null)
            step1Label.Text = LocalizationService.Get("chatOptIn.progress.step1");

        var step2Label = this.FindControl<TextBlock>("Step2Label");
        if (step2Label is not null)
            step2Label.Text = LocalizationService.Get("chatOptIn.progress.step2");

        var recommendedBadge = this.FindControl<TextBlock>("RecommendedBadge");
        if (recommendedBadge is not null)
            recommendedBadge.Text = LocalizationService.Get("chatOptIn.recommended.badge");

        var heroRationale = this.FindControl<TextBlock>("HeroRationale");
        if (heroRationale is not null)
            heroRationale.Text = LocalizationService.Get("chatOptIn.recommended.defaultRationale");

        var stepHeaderKicker = this.FindControl<TextBlock>("StepHeaderKicker");
        if (stepHeaderKicker is not null)
            stepHeaderKicker.Text = LocalizationService.Get("chatOptIn.step1.kicker");

        var stepHeaderTitle = this.FindControl<TextBlock>("StepHeaderTitle");
        if (stepHeaderTitle is not null)
            stepHeaderTitle.Text = LocalizationService.Get("chatOptIn.step1.experimentalHeader");

        var step1Body = this.FindControl<TextBlock>("Step1Body");
        if (step1Body is not null)
            step1Body.Text = LocalizationService.Get("chatOptIn.step1.body1");

        var step1BodySub = this.FindControl<TextBlock>("Step1BodySub");
        if (step1BodySub is not null)
            step1BodySub.Text = LocalizationService.Get("chatOptIn.step1.body2");

        var cancelBtn = this.FindControl<Button>("Step1CancelBtn");
        if (cancelBtn is not null)
            cancelBtn.Content = LocalizationService.Get("chatOptIn.step1.cancelButton");

        var continueBtn = this.FindControl<Button>("Step1ContinueBtn");
        if (continueBtn is not null)
            continueBtn.Content = LocalizationService.Get("chatOptIn.step1.continueButton");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
