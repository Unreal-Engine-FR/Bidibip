const CONFIG = require('./config')
const LOGGER = require('./logger')
const DI = require('./discord_interface')
const MODULE_MANAGER = require('./module_manager')


LOGGER.init_logger()

/*
GIT AUTO-UPDATER
 */
const AutoGitUpdate = require('auto-git-update')
const {Message} = require("./utils/message");
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
                DI.init(client, updater)
                MODULE_MANAGER.get().init()
                new Message()
                    .set_text('Coucou tout le monde ! :wave:')
                    .set_channel(CONFIG.get().LOG_CHANNEL_ID)
                    .send()
                    .catch(err => console.fatal(`failed to send welcome message : ${err}`))
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
