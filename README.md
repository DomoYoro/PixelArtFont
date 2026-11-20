# Pixel Art Font Tool
A unity tool for creating TMP (TextMeshPro) font assets out of textures.

This is a tool I developed out of necessity for Soda-Powered Penguin, as Unity's TMP does not offer any options to create a font from a texture. This is particularly bad for pixel art fonts.
I have not previously released any other tools, so if I'm missing anything, feel free to contribute.

# How to use
Start by creating a `PixelArtFontDirectory` in an editor folder (Create/Text/Pixel Art Font Directory), and assign `PixelArtFontAnalyzer` to the `Analyzer Compute Shader` field of the directory. This directory is necessary as it holds the compute shader reference, which quickly calculates properties about a font from a given texture, and it serves as a quick way to rebuild font assets if the source texture changes.

Once the directory is created, you can start creating `PixelArtFontAsset`s. (Create/Text/Pixel Art Font Asset) These are editor-only assets that build the data necessary for a TMP font asset to read from a custom-generated atlas. You'll primarily want to edit this object through the supplied window, (Window/Pixel Art Font/Pixel Art Font Builder) as this displays information in a more helpful manner.

The given texture for a `PixelArtFontAsset` must have all glyphs (characters) in a grid, and each glyph must be at the same height within the cell as it would be within a line of text. You'll also want to assign a font family name for this unique font. Once this is done, you'll probably want to hit the "Rebuild Data" button in the top right to ensure that the grid slicing & glyph assignment are up-to-date. After that, you can hit "Build" to build the TMP Font Asset that you'll be using in your game, with the glyph atlas and material included. For pixel-perfect aligned results, you can add the included `TextPixelSnap` component to your text object.

That's all- I hope you find good use from this tool!
