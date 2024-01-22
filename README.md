# Lethal Suits
> _A Mod for Lethal Company_

Lethal Suits is a mod that gives players the ability to not only use custom suits, but sync them between each other. To facilitate this, the mod currently queries an object store to dynamically download suits upon loading into the game.

This mod was heavily inspired by the [More_Suits](https://thunderstore.io/c/lethal-company/p/x753/More_Suits/) mod by [x753](https://thunderstore.io/c/lethal-company/p/x753/); it aims to build on it by extending its baseline suit override functionality with a system for loading suits on the fly.

### Usage
After downloading the mod, if you are still using More_Suits, I would recommend disabling it for now: the interplay between the two hasn't been tested much yet.

The mod operates on two kind of config files: your own suits as a player that you'd like to see on the rack, and suits from your friends that you don't want cluttering up your rack.

To configure these files, first run the game and load into a lobby and the files will be created. Then, visit your Thunderstore plugins for Lethal Company, likely under a path like `C:\Users\your_user\AppData\Roaming\Thunderstore Mod Manager\DataFolder\LethalCompany`. Open up your default profile folder, the `BepInEx` folder, and finally `config`. You should see a `suits` directory: open it and you'll find a folder named `friends` and a `suits.txt`.
#### The `suits.txt` file format
Right now, the capability of `suits.txt` is extremely limited. You can only add names of suits from the remote object store, one per line. For testing, the only two suits available right now are `bmo` and `peter-griffin`. Therefore, a config that fetches both of these suits would be:
```
bmo
peter-griffin
```

#### The `friends` folder
Getting suits from your friends is unfortunately done manually right now. Have each of your friends send over their config file and place each in the `friends` directory under some unique name, e.g.: `alice.txt`, `bob.txt`, etc. The names don't have to match anything, they just need to be unique. This will result in their suits being downloaded, but not populated on your rack. Since all suits are loaded, however, you should see their suit when they change!

### Development Feedback
This is an evolving project that will require a lot of testing before it's stable, so patience is appreciated. I also welcome user feedback, from anything to issues you've noticed to any suggestions you might have. The best way to share feedback is by [creating a Github issue](https://github.com/pattyjogal/LethalSuits/issues/new) on the project repository.

### Roadmap
Some items I want to work on in the near future
- [ ] Web interface for browsing and uploading suits
- [ ] Converter for existing suit mods / figuring out proper attribution for suit designers
- [ ] Automatic syncing of suits when a new player joins with the mod
- [ ] More extensive config file with options
