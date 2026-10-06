# Every editor, honestly

The only hard requirement in this workspace is the terminal:

```
dotnet build mods/<ModName>/<ModName>.csproj
```

Editors decide how comfortable writing the code feels; they never decide whether it builds. Three tiers:

| Tier | Editors | You get |
|---|---|---|
| Full IDE | Visual Studio Community, Rider, VS Code | Autocomplete, inline errors, go-to-definition into game code |
| LSP terminal editors | Neovim, Helix, Vim, Zed (experimental) | Same intelligence, keyboard-first, a setup snippet instead of an installer |
| Text tier | Notepad++, Notepad, anything else | Syntax highlight at best; build in the terminal |

**One feature worth the setup in every tier:** once `LucidCatsDir` points at the game (MODDING.md section 1), go-to-definition jumps into the game's own classes, because the `.csproj` files reference the real game DLLs.

---

## Visual Studio Community (Windows)

1. Install from https://visualstudio.microsoft.com/ with the **.NET desktop development** workload.
2. Open the repo folder (Open a Local Folder). It understands `.csproj`, `Directory.Build.props` and `global.json` by itself.
3. Build in the built-in terminal with `dotnet build build.proj`.

Free for individuals, students and open-source.

## JetBrains Rider (Windows, macOS, Linux)

1. Install from https://www.jetbrains.com/rider/.
2. Open the repo folder.

Free for **non-commercial** use since 2024; many people still believe it is paid, but modding a game qualifies for the free license.

## Visual Studio Code (Windows, macOS, Linux)

1. Install VS Code, then the **C# Dev Kit** extension (it pulls in the **C#** extension).
2. Open the repo folder. This repo ships `.vscode/extensions.json` (VS Code will offer to install the right extensions) and `.vscode/tasks.json`; `Terminal → Run Build Task` (Ctrl+Shift+B) runs `dotnet build build.proj` for you.

The *C#* extension is fully free; *C# Dev Kit* uses the Visual Studio Community license, free for individuals, students, open-source and companies with up to 5 developers. Every hobby modder is on the free side. If you want zero license questions at all, DotRush or the plain C# extension works too.

## Neovim

With a plugin manager plus `nvim-lspconfig` and Mason:

```lua
-- in your lua config, after mason is set up:
require("lspconfig").omnisharp.setup({})
```

Then `:MasonInstall omnisharp` and open any `.cs` file. Formatting follows the repo's `omnisharp.json` and `.editorconfig` (tabs). No repo files needed; the server config lives in your dotfiles.

## Helix

Download the **official OmniSharp-Roslyn release** (https://github.com/OmniSharp/omnisharp-roslyn/releases; the brew build is broken for this, take the official one) and unzip it somewhere stable. Then in `~/.config/helix/languages.toml`:

```toml
[language-server.omnisharp]
command = "/path/to/omnisharp-roslyn/run"
args = ["-lsp"]
timeout = 10000
```

First run per project takes a few seconds while the server indexes the game DLLs.

## Vim

With vim-plug:

```vim
Plug 'OmniSharp/omnisharp-vim'
let g:OmniSharp_server_use_net6 = 1
```

Run `:PlugInstall`, then `:OmniSharpInstall` once. Works fine; clunkier than the above.

## Zed

OmniSharp language-server support exists but is young; treat as experimental. Same server download as Helix; configure via Zed's extension settings. If you make OmniSharp behave on Zed, the snippet is welcome as a contribution.

## Notepad / Notepad++ (and anything else)

Open the file, edit, save. Notepad++ highlights C# automatically once the file ends in `.cs`. Then build in the terminal: the same `dotnet build` as everyone else. This tier is fine for a two-line tweak. For anything bigger than a tweak, one of the editors above is worth the setup.

---

## Picking one

New to all of this: Visual Studio Community. Unity smarts: Rider. Already in VS Code: C# Dev Kit. Already in a terminal editor: the snippet above.
