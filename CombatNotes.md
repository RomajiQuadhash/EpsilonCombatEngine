Why are we looking at copying all of these classes?
It is so that we can duplicate a "combat" (later holding class) to simulate events if a user picks some option. 

To do that smoothly, we'll need to ensure that all the classes involved in the combat simulation can all be duplicated and re-linked to each other systematically.
The interfaces added will tell later classes how to duplicate those classes and how to re-link them.