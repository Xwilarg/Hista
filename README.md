# Hista
Discord bot for role management: basically you have a message, you add an emote, it give you a role.

## How does it work
To begin with, create a file named token.txt in a 'Keys' folder located next to your executable, please your bot token inside it.<br/>
Once you done that, you need to understand that there are two kind of role assignation:

### Roles
They are basic roles that can be assigned to an user, you must have a "roles.json" file in the 'Keys' folder, next toyour token.txt file.<br/>
Your JSON must be an array under the name roles and for each element contains the following key:
  - emote: The emote unicode character
  - guildId: ID of the guild containing the message giving the role
  - channelId: ID of the channel containing the message giving the role
  - messageId: ID of the message giving the role
  - roleId: Role given

Example of file:
```
{
  "roles": [
    {
      "emote" : "❗",
      "guildId": 146701031227654144,
      "channelId": 599581908963426304,
      "messageId": 602940259860348968,
      "roleId": 599575719541997568
    }
  ]
}
```

### Factions
Factions are roles where only one of them can be given at the same time.<br/>
That means that if an user try to get a second one, he will loose the first one.<br/>
The JSON name is "factions.json" and is located next to your token file.<br/>
It contains the file values as roles.txt, plus a "defaultRole" array containing for each element:
  - guildId: Guild concerned by this default role
  - roleId: Default role ID

If a default role is set, it'll be assigned to everyone when the bot is launched.<br/>
If an user quit his faction, the default role will be assigned to him.
