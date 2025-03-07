# UndertaleModTool Community Edition

A fork of [UndertaleModTool](https://github.com/UnderminersTeam/UndertaleModTool) with a lot of features for Pizza Tower modding and stuff.

## Features
- A tile editor
- Can open gml_Object_obj_player_Step_0
- Anonymous function support
- Self call support (`self.func()`)
- Automatic selection/creation of instance layers and grid snapping when dragging objects into rooms
- Button to add creation code to rooms and objects in rooms
- Room editor improvements: middleclick-drag scrolling, scaling objects by dragging their edges, easy creation of creation code, and more
- Many other compiler/decompiler fixes and features
- And more to come in the future!

## Download

There are a few ways to get UTMTCE:
- 1: Download the current release build from [GameBanana](https://gamebanana.com/tools/14193/)
- 2: In v0.5.5 and above, go to Settings -> Download latest dev build to update to the latest dev build (GitHub commit, I don't know why we're calling them dev builds).
- 3: Get a dev build from [Github Actions artifacts](https://github.com/XDOneDude/UndertaleModToolCE/actions/) (requires a GitHub account)
- 4: Compile it yourself; the steps are [the same as in vanilla UTMT](https://github.com/krzys-h/UndertaleModTool#compilation-instructions)

## Credits
UndertaleModTool and its forks are open-source and licensed under [GPLv3](https://github.com/UnderminersTeam/UndertaleModTool/blob/master/LICENSE.txt), so they can be used without needing to ask for permission, with required credit (and source code, which is right here).

- [The Underminers team](https://github.com/UnderminersTeam): made the original UndertaleModTool
- [CST1229](https://github.com/CST1229): "Lead programmer"; most changes and features, application icon
- [FlashNin](https://github.com/XDOneDude): Repository owner
- Authors of forks (that weren't already merged into vanilla UTMT):
  - [AwfulNasty](https://github.com/AwfulNasty): Room editor changes
  - [SrPerez](https://github.com/GithubSPerez): Additional GML functions, inspiration for struct support

<!--
  commandline building:

  dotnet publish UndertaleModTool -c Release -r win-x64 --self-contained false -p:PublishSingleFile=True --output bin/non-sc
  dotnet publish UndertaleModTool -c Release -r win-x64 --self-contained true -p:PublishSingleFile=True --output bin/sc
  dotnet publish UndertaleModCli -c Release -r win-x64 --self-contained false -p:PublishSingleFile=True --output bin/cli

-->
