const CONFIG = require("../config.js").get()
const {REST, ActivityType, SlashCommandBuilder, Routes} = require("discord.js")
const Discord = require("discord.js");

/**
 * @type {DiscordInterface}
 */
let DISCORD_CLIENT = null

class DiscordInterface {
    constructor(client, updater) {
        this._client = client
        this._updater = updater

        this.on_message = null
        this.on_message_delete = null
        this.on_message_update = null
        this.on_interaction = null

        client.on(Discord.Events.MessageCreate, this._on_message)
        client.on(Discord.Events.MessageDelete, this._on_message_delete)
        client.on(Discord.Events.MessageUpdate, this._on_message_udpate)
        client.on(Discord.Events.InteractionCreate, this._on_interaction)

        this.module_manager = null
    }

    _on_message(msg) {
        if (DISCORD_CLIENT.on_message && !msg.author.bot)
            DISCORD_CLIENT.on_message(msg)
    }

    _on_message_delete(msg) {
        if (DISCORD_CLIENT.on_message_delete && !msg.author.bot)
            DISCORD_CLIENT.on_message_delete(msg)
    }

    _on_message_udpate(old_msg, new_msg) {
        if (DISCORD_CLIENT.on_message_update && !old_msg.author.bot) {
            DISCORD_CLIENT.on_message_update(old_msg, new_msg)
        }
    }

    _on_interaction(interaction) {
        if (DISCORD_CLIENT.on_interaction && !interaction.user.bot)
            DISCORD_CLIENT.on_interaction(interaction)
    }

    async get_user_count() {
        const guild = await this._client.guilds.fetch(CONFIG.SERVER_ID)
        const members = await guild.members.fetch()
        return members.filter(member => member.user.bot === false).size
    }

    set_activity(message) {
        this._client.user.setPresence({activities: [{name: message, type: ActivityType.Watching}]})
    }

    async set_slash_commands(commands) {
        const command_data = []
        for (const command of commands) {
            const discord_command = new SlashCommandBuilder()
                .setName(command.name)
                .setDescription(command.description)

            if (command._member_only || command._admin_only)
                discord_command.setDMPermission(false)

            discord_command.setDefaultMemberPermissions(command._min_permissions)

            for (const option of command.options) {
                switch (option.type) {
                    case 'text':
                        discord_command.addStringOption(opt => {
                            opt.setName(option.name)
                                .setDescription(option.description)
                                .setRequired(option.required)
                            for (const choice of option.choices)
                                opt.addChoices({name: choice, value: choice})
                            return opt
                        })
                        break
                    case 'bool':
                        discord_command.addBooleanOption(opt =>
                            opt.setName(option.name)
                                .setDescription(option.description)
                                .setRequired(option.required))
                        break
                    case 'user':
                        discord_command.addUserOption(opt =>
                            opt.setName(option.name)
                                .setDescription(option.description)
                                .setRequired(option.required)
                        )
                        break
                }
            }
            command_data.push(discord_command.toJSON())
        }

        // Construct and prepare an instance of the REST module
        const rest = new REST().setToken(CONFIG.APP_TOKEN); // don't remove this semicolon

        // deploy old
        try {
            console.info(`Started refreshing ${command_data.length} application (/) commands.`)

            // The put method is used to fully refresh all old in the guild with the current set
            const data2 = await rest.put(
                Routes.applicationCommands(CONFIG.APP_ID),
                {body: command_data},
            )
            console.info(`Successfully reloaded ${data2.length} application (/) commands.`)
        } catch (error) {
            // And of course, make sure you catch and log any errors!
            console.error(error)
        }
    }

    async check_updates() {
        if (!this._updater)
            console.fatal('updater is null')
        return await this._updater.compareVersions().catch(err => {
            console.fatal(`Failed to get version ${err}`)
        })
    }
}

function init(client, updater) {
    DISCORD_CLIENT = new DiscordInterface(client, updater)
}

/**
 * Get discord interface
 * @returns {DiscordInterface}
 */
function get() {
    if (!DISCORD_CLIENT)
        console.fatal('discord client have not been initialized yet')
    return DISCORD_CLIENT
}

module.exports = {init, get}