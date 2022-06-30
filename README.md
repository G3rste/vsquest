# VS Quest

Die Mod aims to add Quests to Vintage Story.<br>
It should also enable you to easily add your own quests to the game as well as your own questgivers.<br>
<br>
If you want to use this as a base for creating your own quests, please have a look at this **[example](example)**. The most important aspects to take care of are the **[quests.json](example/assets/vsquestexample/config/quests.json)** as well as the **[questgiver behavior](example/assets/vsquestexample/entities/questgiver.json#L229-L235)**<br><br>
Every quest in the quests.json can have the following attributes:
* **id**: Unique id to identify your quest in the system
* **cooldown**: cooldown in days until the questgiver offers the quest again
* **predecessor**: optional -> questid that has to be completed before this quest becomes available
* **perPlayer**: determines if the quest cooldown is set per player or globally
* **gatherObjectives**: list of items the player has to offer
  * **validCodes**: list of accepted item codes
  * **demand**: needed amount
* **killObjectives**: list of entities the player has to defeat
  * **validCodes**: list of accepted entity codes
  * **demand**: needed amount
* **itemRewards**: list of items the player receives upon completing the quest
  * **itemCode**: code of the reward
  * **amount**: amount the player receives

To convert an entity to a questgiver it needs the questgiver behavior:
* **quests**: list of quests the questgiver offers
* **selectrandom**: if set to true, the questgiver will only offer a random selection of its quests
* **selectrandomcount**: determines the number of random quests the questgiver offers

![Thumbnail](resources/modicon.png)