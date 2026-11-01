Box2D 2.3.1 library sources from https://github.com/erincatto/box2d
Tag v2.3.1, commit 7e633c4fb86a68bf072fb8ae67ea2c060114750e.
Only Box2D/Box2D library sources and the upstream license are vendored.
KNL alteration: Dynamics/b2Island.cpp accepts the game's serialized linear/angular
speed caps during both regular and TOI integration. The change is explicitly marked;
all other upstream library files are unchanged. This prevents using the upstream
90-degrees-per-step safety bound in place of Unity's 360-degrees-per-second setting.
The clean-room bridge lives separately in ../../knlpouch.cpp.
This is a compatibility approximation of Unity's pouch solver; identical Unity results
are not guaranteed. No proprietary game outlines or game logic are included here.
