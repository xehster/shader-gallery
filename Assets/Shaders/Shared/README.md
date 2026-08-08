# Shared shaders

A copy of `Assets/Shaders` from [unity-shaders](https://github.com/xehster/unity-shaders),
brought here by the `Sync shaders` workflow. `UPSTREAM.txt` says which commit.

Don't edit anything in this folder. The next sync deletes it and writes it again, so your
changes go with it.

To change one of these shaders, copy the file into `Assets/Shaders/Local/` without its
`.meta` (Unity will make a new one), and rename the shader on the first line, say
`Shader "Local/PS1 Lit"`. Now it's yours: the copy shows up in the gallery next to the
original and nothing overwrites it.

Your own shaders belong in `Assets/Shaders/Local/`.
