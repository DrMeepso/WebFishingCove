> [!NOTE]  
> 🐟

it doesn't require Godot or anything other than Steamworks.

# Webfishing Cove

Cove is a dedicated server for WebFishing written in C#.

# Features

- Lightweight and fast – Because of the lightweight nature of the server, it can run with less than 1 GB of RAM with 20+ players.
- Moderation – Moderation is built into the Cove.ChatCommands plugin bundled with the server.
- Plugins – Plugins have full access to the internal server classes, allowing for extreme customization.
- .NET – Cove is written in C# on the .NET framework, making plugin development easy, fast, and accessible.
- Support – Cove has a Discord server for fast support for anything related to Cove (including plugin development).

# How it works

Cove uses none of WebFishing's code. Instead, it uses C# to emulate the same network calls that the official game makes to host a lobby.

Things like event spawning all had to be written from scratch to allow for the portability of C#.

Because of this emulation, to run the server you must use a Steam account that owns the game and has Steam open in the background.

If you have any questions or issues with Cove, **create an issue on GitHub** or join the [**Discord Server**](https://discord.gg/QfydV2Ze8f).

# Todo

- [x] Spawn actors required for the metal detector
- [x] Improve error handling
- [x] Add plugin / modding support (C# API)
- [x] Add proper support for actor handling
- [x] Make hostspawn and metalspawn IHostedService instances
- [x] Write a plugin guide / how to create plugins (can be found here: [Plugins.md](./Plugins.md))

# How to run

> [!NOTE]  
> To run a server, you must have Steam open on the computer you wish to run the server on,  
> and Steam must be logged into an account that has WebFishing in its library.
>
> Also note that you can't join the server on the same account that is hosting it.

1. Download
   - You can download the most recent version of the server here: [Nightly Releases](https://github.com/DrMeepso/WebFishingCove/tags)
   - Or, if you want the latest stable version, it is here: [Latest Release](https://github.com/DrMeepso/WebFishingCove/releases/latest)
   - A new build is made every time the code is changed, so it may update quite often.
   - Also make sure you have the .NET 8 runtime installed. You can download it [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

2. Change settings
   - If you don’t see the config files (`server.cfg` & `admins.cfg`), run the server once and they should be created in the same directory as the application.
   - You can modify the settings in the `server.cfg` file with all the information you want.
   - To add an admin, put their Steam64 ID in the `admins.cfg` file with `= true` after it.
   - Example: `76561198288728683 = true`

3. Run
   - Run the server and enjoy.
   - Please be respectful and don’t name the server anything stupid.

4. Look below
   - Links for finding or creating plugins are below.

# Other info

Some default / popular plugins can be found in the [CovePlugins](https://github.com/DrMeepso/CovePlugins) repo.

The repository for a template plugin can be found here: [CovePluginTemplate](https://github.com/DrMeepso/TemplateCovePlugin).

More plugins can be found in the Cove Discord server listed above.
