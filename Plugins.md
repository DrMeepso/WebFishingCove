# Plugins

## Loading / Installing

- Run CoveServer once to generate the `/plugins` directory.
- Make sure you have plugins enabled in the `server.cfg` file!
- Place the plugin dll file in the `/plugins` directory.
- Restart CoveServer.
- The plugin should now be loaded!
- If the plugin is not loaded, check the CoveServer log for errors.

## Creating Plugins

The repo for a template plugin can be found here: [CovePluginTemplate](https://github.com/DrMeepso/TemplateCovePlugin)

- Make sure you have .Net 8.0 installed. (Cove uses .net 8!)
- Create a new class library project in your IDE of choice.
- Add a reference to the `Cove.dll` file, this can be found in the CoveServer directory.
- If you didn't build Cove yourself, you can download this repo and reference it as a project in your solution.
- Create a class that extends the `CovePlugin` class.
- Implement the `onInit` method.
- Add a `plugin.cfg` file to the project
- Make sure the plugin.cfg file is set to bundle into the dll. (This makes plugins compact and work with just one file!)
  - You can do this by adding XML to the `.csproj` file.
    ```xml
        <ItemGroup>
            <EmbeddedResource Include="plugin.cfg"></EmbeddedResource>
        </ItemGroup>
    ```
- Build the project!

## Example Plugin

This is a simple plugin that logs when it is loaded and when a chat message is received.

```csharp
using Cove.Server;
using Cove.Server.Actor;
using Cove.Server.Plugins;
using Steamworks;

public class ChatCommands : CovePlugin
{
    CoveServer Server { get; set; }
    public ChatCommands(CoveServer server) : base(server)
    {
        Server = server;
    }

    public override void onInit()
    {
        base.onInit();
        Log("Plugin loaded!");
    }

    public override void onEnd()
    {
        base.onEnd();
        // make sure you unload anything or stop any threads here!
        // this is called when !reload is called or when the server ends
        // if you wanna make sure your plugin reloads properly, make sure to clean up here!
        Log("Plugin unloaded!");
    }

    public override void onChatMessage(WFPlayer sender, string message, bool local, Vector3 position, string zone)
    {
        base.onChatMessage(sender, message);
        Log($"{sender.Username}: {message}");
    }

}
```

Here is the `plugin.cfg` file for the plugin.

```cfg
name=Plugin Name
author=Your Name
version=1.0
description=Simple chat logging plugin
```

## Plugin API

You can see all methods and properties available in the `CovePlugin` class here: [CovePlugin.cs](https://github.com/DrMeepso/WebFishingCove/blob/main/Cove/Server/Plugins/CovePlugin.cs)
You can also use the `Server` property to access the CoveServer instance and all of its properties and methods!
Most useful methods are in the [Server.Utils.cs](https://github.com/DrMeepso/WebFishingCove/blob/main/Cove/Server/Server.Utils.cs) file
But anything in the server class is usable!
