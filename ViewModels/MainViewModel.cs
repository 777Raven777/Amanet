using System;
using System.Collections.Generic;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels;

/// <summary>
/// One drawer of the medicine cabinet (a sidebar entry).
/// Plain record, not a ViewModel — it only describes a destination;
/// the destination itself is <see cref="Content"/>.
/// </summary>
public sealed class SectionItem
{
    public required string Glyph { get; init; }      // kanji "label on the drawer"
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required ViewModelBase Content { get; init; }
}

public partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    // Apothecary's daily wisdom. Rendered once per app launch in the footer.
    private static readonly string[] Omens =
    {
        "Today's tea has been tested for poison. Sadly, negative.",
        "Remember: the dose makes the poison. Applies to messages too.",
        "If a letter smells of bitter almonds, do not open it.",
        "Honey makes a lovely gift. Unless you know your history.",
        "Ox bezoar in stock. Do not ask why she's so happy about it.",
        "Eat, sleep, identify toxins. In that order.",
    };

    public IReadOnlyList<SectionItem> Sections { get; }

    [ObservableProperty]
    private SectionItem? _selectedSection;

    public string Omen { get; } = Omens[Random.Shared.Next(Omens.Length)];

    // TODO: replace with ISessionService once login stores the real user + token.
    public string CurrentUsername => "猫猫";
    public string CurrentRank => "apothecary on duty";

    public MainViewModel(
        INavigationService navigation,
        ServersSectionViewModel servers,
        ConversationsSectionViewModel conversations,
        FriendsSectionViewModel friends,
        InvitesSectionViewModel invites,
        ProfileSectionViewModel profile)
    {
        _navigation = navigation;

        Sections =
        [
            new SectionItem { Glyph = "殿", Title = "Servers",       Subtitle = "Pavilions you belong to",            Content = servers },
            new SectionItem { Glyph = "文", Title = "Conversations", Subtitle = "Private correspondence",             Content = conversations },
            new SectionItem { Glyph = "友", Title = "Friends",       Subtitle = "Allies, informants, taste-testers",  Content = friends },
            new SectionItem { Glyph = "簡", Title = "Invites",       Subtitle = "Summons from other pavilions",       Content = invites },
            new SectionItem { Glyph = "我", Title = "Profile",       Subtitle = "Your file in the palace records",    Content = profile },
        ];

        SelectedSection = Sections[0];
    }

    [RelayCommand]
    private void Logout()
    {
        // TODO: ISessionService.Clear() + (optionally) hit a token-revocation endpoint.
        _navigation.NavigateTo<LoginViewModel>();
    }

    partial void OnSelectedSectionChanged(SectionItem? value)
    {
        if (value?.Content is IActivatable activatable)
            _ = activatable.ActivateAsync();
    }
}