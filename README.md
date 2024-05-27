# UndertaleModTool Community Edition

A fork of [UndertaleModTool](https://github.com/UnderminersTeam/UndertaleModTool) with a lot of features for Pizza Tower modding and stuff.

## Features
- A tile editor
- Can open gml_Object_obj_player_Step_0
- Struct support in the compiler
- Anonymous function support
- Self call support (`self.func()`)
- Automatic selection/creation of instance layers and grid snapping when dragging objects into rooms
- Support for the GMS2.3.7+ boolean type, so true/false no longer turns into 1 and 0
- Button to add creation code to rooms and objects in rooms
- Room editor improvements: middleclick-drag scrolling, scaling objects by dragging their edges, easy creation of creation code, and more
- Many other compiler/decompiler fixes and features
- And more to come in the future!

## Download

There are a few ways to get UTMTCE:
- 1: Download the current release build from [GameBanana](https://gamebanana.com/tools/14193/)
- 2: Get the latest dev build from [Github Actions artifacts](https://github.com/XDOneDude/UndertaleModToolCE/actions/)
- 3: Compile it yourself; the steps are [the same as in vanilla UTMT](https://github.com/krzys-h/UndertaleModTool#compilation-instructions)

## Credits
UndertaleModTool and its forks are open-source and licensed under [GPLv3](https://github.com/UnderminersTeam/UndertaleModTool/blob/master/LICENSE.txt), so they can be used without credit, without needing to ask for permission.

- [The Underminers team](https://github.com/UnderminersTeam): made the original UndertaleModTool
- [CST1229](https://github.com/CST1229): "Lead programmer"; most features, application icon
- [FlashNin](https://github.com/XDOneDude): Repository owner
- Authors of used forks:
  - [AwfulNasty](https://github.com/AwfulNasty): Room editor changes
  - [Jacky720](https://github.com/Jacky720): Misc. changes (e.g decompiler parenthesis cleanup)
  - [SrPerez](https://github.com/GithubSPerez): Additional GML functions, inspiration for struct support

<!--
  commandline building:

  dotnet publish UndertaleModTool -c Release -r win-x64 --self-contained false -p:PublishSingleFile=True --output bin/non-sc
  dotnet publish UndertaleModTool -c Release -r win-x64 --self-contained true -p:PublishSingleFile=True --output bin/sc
  dotnet publish UndertaleModCli -c Release -r win-x64 --self-contained false -p:PublishSingleFile=True --output bin/cli

-->
