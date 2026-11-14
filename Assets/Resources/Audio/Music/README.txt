Place any main-menu music files in this folder. Prefer MP3, OGG, or WAV;
MP3 is the safest choice for Unity on Windows. M4A/AAC may import but fail
to decode depending on the codec.

Names are not fixed. Prefixing them keeps playlist order predictable, e.g.:
theme_music_0_shinzou_wo_sasageyo.m4a
theme_music_1_another_song.mp3

MainMenuController loads every AudioClip here, plays them as a looping
playlist, and applies the Music Volume setting.
