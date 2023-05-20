const CONFIG = require('./config')
const LOGGER = require('./logger')
const {init} = require('./discord_interface')
const MODULE_MANAGER = require('./module_manager')


LOGGER.init_logger()

/*
GIT AUTO-UPDATER
 */
const AutoGitUpdate = require('auto-git-update')
const Discord = require("discord.js")
const updater = new AutoGitUpdate({
    repository: 'https://github.com/Unreal-Engine-FR/Bidibip',
    branch: CONFIG.get().UPDATE_FOLLOW_BRANCH,
    tempLocation: CONFIG.get().CACHE_DIR + '/updater/',
    exitOnComplete: true
})

updater.autoUpdate()
    .then(result => {
        if (result) {
            console.validate('Application up to date !')

            /*
            CREATE DISCORD CLIENT
             */
            const Discord = require('discord.js')
            const client = new Discord.Client(
                {
                    partials: [Discord.Partials.Channel],
                    intents: [
                        Discord.GatewayIntentBits.Guilds,
                        Discord.GatewayIntentBits.GuildMessages,
                        Discord.GatewayIntentBits.GuildMembers,
                        Discord.GatewayIntentBits.MessageContent,
                        Discord.GatewayIntentBits.GuildMessageReactions,
                        Discord.GatewayIntentBits.DirectMessages
                    ]
                }
            )

            /*
            START DISCORD CLIENT
             */
            client.on('ready', () => {
                init(client, updater)
                MODULE_MANAGER.get().init(client)
                client.channels.cache.get(CONFIG.get().LOG_CHANNEL_ID).send({content: 'Coucou tout le monde ! :wave: '})
            })
            client.login(CONFIG.get().APP_TOKEN)
                .then(_token => {
                    console.validate(`Successfully logged in !`)
                })
                .catch(error => console.fatal(`Failed to login : ${error}`))

        }
        else {
            console.warning('Application outdated, waiting for update...')
        }
    })
    .catch(err => console.error(`Update failed : ${err}`))
