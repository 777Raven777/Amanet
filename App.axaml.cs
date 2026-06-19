using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AvaloniaApplication1;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // infrastructure
        services.AddSingleton<ISession, Session>();
        services.AddHttpClient<IAuthService, AuthService>(c =>
            c.BaseAddress = new Uri("http://localhost:8181/"));

        services.AddTransient<AuthHeaderHandler>();

        services.AddHttpClient<IFriendService, FriendService>(c =>
            c.BaseAddress = new Uri("http://localhost:8181/"))
            .AddHttpMessageHandler<AuthHeaderHandler>();

        services.AddHttpClient<IConversationService, ConversationService>(c =>
            c.BaseAddress = new Uri("http://localhost:8181/"))
            .AddHttpMessageHandler<AuthHeaderHandler>();

        services.AddHttpClient<IServerInvitesService, ServerInvitesService>(c =>
            c.BaseAddress = new Uri("http://localhost:8181/"))
            .AddHttpMessageHandler<AuthHeaderHandler>();

        services.AddHttpClient<IProfileService, ProfileService>(c =>
            c.BaseAddress = new Uri("http://localhost:8181/"))
            .AddHttpMessageHandler<AuthHeaderHandler>();

        services.AddHttpClient<IServerService, ServerService>(c =>
            c.BaseAddress = new Uri("http://localhost:8181/"))
            .AddHttpMessageHandler<AuthHeaderHandler>();

        services.AddSingleton<IChatHub>(sp =>
            new ChatHubClient(
                sp.GetRequiredService<ISession>(),
                new Uri("http://localhost:8181/")));

        //addded
        /*services.AddSingleton<Func<Guid, Guid, ChannelChatViewModel>>(sp =>
            (serverId, channelId) => new ChannelChatViewModel(
                    sp.GetRequiredService<IChatSocket>(), serverId, channelId));*/

        services.AddSingleton<INavigationService>(sp =>
            new NavigationService(t => (ViewModelBase)sp.GetRequiredService(t)));

        services.AddSingleton<IFilePickerService, FilePickerService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();

        // Main shell + its sections. Transient: a fresh main page after every login,
        // so no stale state survives a logout (the palace keeps no ghosts).
        services.AddTransient<MainViewModel>();
        services.AddTransient<FriendsSectionViewModel>();
        services.AddTransient<InvitesSectionViewModel>();
        services.AddTransient<ProfileSectionViewModel>();
        services.AddTransient<ServersSectionViewModel>();
        services.AddTransient<ConversationsSectionViewModel>();

        // services.AddSingleton<IAuthService, AuthService>();

        var provider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Required with CommunityToolkit.Mvvm — otherwise Avalonia's
            // DataAnnotations validator duplicates the toolkit's and can
            // interfere with bindings. This was commented out; it shouldn't be.
            //DisableAvaloniaDataAnnotationValidation();

            desktop.MainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    //private static void DisableAvaloniaDataAnnotationValidation()
    //{
    //    var toRemove = BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
    //    foreach (var plugin in toRemove)
    //        BindingPlugins.DataValidators.Remove(plugin);
    //}
}