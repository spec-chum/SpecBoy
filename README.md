# SpecBoy
Game Boy emulator badly written in C#

I've gone for reasonable (M cycle) accuracy across all components meaning games like the infamous Pinball Deluxe work fine. <img src=https://cdn.discordapp.com/attachments/748230457195495505/769610614829350942/unknown.png> </img>

Scanline renderer only for now, so no mid-scanline effects for Prehistorik Man, but I do have a "pseudo FIFO" branch I'm working on which draws 4 pixels at a time and allows the aforementioned Prehistorik Man scroller to display correctly. <img src=https://cdn.discordapp.com/attachments/748230457195495505/767716390819332106/unknown.png> </img>

Now includes a fully functional APU (Audio Processing Unit) with all 4 Game Boy sound channels:
- Square wave channel 1 (with sweep)
- Square wave channel 2 
- Wave channel (programmable waveform)
- Noise channel
- Proper frame sequencer timing and channel mixing
- SDL audio output
