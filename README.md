> [!NOTE]  
> 🐟

# Webfishing Cove
Cove is a dedicated server for WebFishing written in C#!

Cove is a implementation of the WebFishing network protocall in a CLI meaning it dosent require Godot or anything other than Steamworks!

# Fetures
* Lightweight and Fast - Because of the lightweight nature of the server it can run with less than 1gb of ram with 20+ players on the server!
* Moderation - Moderation is built into the Cove.Chatcommands plugin that cames bundled with the server!
* Plugins - Plugins have full access to the internal server class allowing for extreme customisation!
* .Net - Cove is written in C# on the dotnet framework, making plugin development easy, fast and accessible!
* Support - Cove has a discord server for fast support for anything related to cove (including plugin development)

# How it works
Cove uses none of WebFishing's code, insted it used C# to emulate the same network calls that the offical game would make to host a lobby!

Things like event spawning all had to be written from scratch to allow for the portability of C#!

Because of this emulation to run the server you must run it from a steam account that owns the game and that has steam open in the background

If you have any questions or issues with Cove, **create and issue on Github** or join the [**Discord Server**](https://discord.gg/QfydV2Ze8f)

# Todo
- [X] Spawn actors required for the metal detector
- [X] Improve error handling
- [X] Some sort of plugin or modding support (C# API)
- [X] Add proper support for actor handling
- [X] Make hostspawn and metalspawn IHostedService's
- [X] Write a plugin guide / how to create plugins, can be found here [Plugins.md](./Plugins.md)

# How to run:

> [!NOTE]  
> To run a server you must have Steam open on the computer you wish to run the server on
> and Steam must be logged into a account that has WebFishing in it's library 
> 
> Also please note you can't join the server on the account you are hosting it on!

1. Download
	- You can download the most recent version of the server here: [Nightly Releases](https://github.com/DrMeepso/WebFishingCove/tags)
	- Or if you want the latest stable version it is here: [Latest Release](https://github.com/DrMeepso/WebFishingCove/releases/latest)
	- A new build is made everytime code is changed so it may update quite alot!
	- Also make sure you have the .net8 runtime installed, you can download it [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

2. Decompile WebFishing
	- Once you have the source files drag the `main_zone.tscn` file into the `/worlds` folder of the server!
	- The `main_zone.tscn` file can be found here in the WebFishing project `/Scenes/Map`
	- You can use [gdsdecomp](https://github.com/bruvzg/gdsdecomp) to decompile a Godot project (WebFishing)

3. Change settings
	- If you dont see the config files (server.cfg & admins.cfg) run the server once and they should be created in the same place the application is!
	- You can modify the settings in the server.cgf file with all the info you want!
	- Too add a admin put there Steam64ID in the admins.cfg file with a ` = true` after it!
	- I.E. `76561198288728683 = true`

4. Run!
	- Run the server and enjoy! 
	- Please be respectful and dont name the servers anything stupid!

5. Look below
	- Links if you wanna find plugins or make them are below!
	 
# Other info

Some default / popular plugins can be found in the [CovePlugins](https://github.com/DrMeepso/CovePlugins) repo

The repo for a template plugin can be found here: [CovePluginTemplate](https://github.com/DrMeepso/TemplateCovePlugin)

More plugins can be found in the discord server for cove listed above!