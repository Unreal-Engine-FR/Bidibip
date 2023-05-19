const MODULE_MANAGER = require('./module_manager').get()
const CONFIG = require('./config').get()
require('./logger').init()

const {patch_client} = require('./discord_interface')

/*
GIT AUTO-UPDATER
 */
const AutoGitUpdate = require('auto-git-update')
const updater = new AutoGitUpdate({
    repository: 'https://github.com/Unreal-Engine-FR/Bidibip',
    branch: 'dev',
    tempLocation: CONFIG.UPDATE_CACHE,
    exitOnComplete: true
});
updater.autoUpdate()
    .then(result => console.info(result ? `Update complete !` : `Update failed`))
    .catch(err => console.error(`Update failed : ${err}`))

/*
CREATE DISCORD CLIENT
 */
const Discord = require('discord.js');
const client = new Discord.Client(
    {
        partials: [Discord.Partials.Channel],
        intents: [
            Discord.GatewayIntentBits.Guilds,
            Discord.GatewayIntentBits.GuildMessages,
            Discord.GatewayIntentBits.GuildMembers,
            Discord.GatewayIntentBits.MessageContent,
            Discord.GatewayIntentBits.DirectMessages
        ]
    }
)

/*
START DISCORD CLIENT
 */
client.on('ready', () => {
    patch_client(client)
    MODULE_MANAGER.init(client)
    client.channels.cache.get(CONFIG.LOG_CHANNEL_ID).send({content: 'Coucou tout le monde ! :wave: '})
})
client.login(CONFIG.APP_TOKEN)
    .then(_token => {
        console.validate(`Successfully logged in !`)
    })
    .catch(error => console.fatal(`Failed to login : ${error}`))
