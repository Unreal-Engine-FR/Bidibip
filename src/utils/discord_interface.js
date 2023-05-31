const CONFIG = require("../config.js")
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
        this.on_thread_create = null

        client.on(Discord.Events.ThreadCreate, this._on_thread_create)
        //client.on(Discord.Events.ChannelUpdate, channel => console.log('ChannelUpdate :', channel))
        //client.on(Discord.Events.ChannelCreate, channel => console.log('ChannelCreate :', channel))
        client.on(Discord.Events.MessageCreate, this._on_message)
        client.on(Discord.Events.MessageDelete, this._on_message_delete)
        client.on(Discord.Events.MessageUpdate, this._on_message_udpate)
        client.on(Discord.Events.InteractionCreate, this._on_interaction)

        this._everyone_role = null
        this._admin_role = null
        this._member_role = null

        this.module_manager = null
    }

    async check_permissions_validity() {
        let guild = await this._client.guilds.cache.get(CONFIG.get().SERVER_ID)
        if (!guild)
            guild = await this._client.guilds.fetch(CONFIG.get().SERVER_ID)
                .catch(err => console.fatal(`Failed to fetch guild : ${err}`))

        DISCORD_CLIENT._admin_role = await guild.roles.cache.get(CONFIG.get().ADMIN_ROLE_ID)
        if (!DISCORD_CLIENT._admin_role)
            DISCORD_CLIENT._admin_role = await guild.roles.fetch(CONFIG.get().ADMIN_ROLE_ID)
                .catch(err => console.fatal(`Failed to fetch admin role : ${err}`))

        DISCORD_CLIENT._member_role = await guild.roles.cache.get(CONFIG.get().MEMBER_ROLE_ID)
        if (!DISCORD_CLIENT._member_role)
            DISCORD_CLIENT._member_role = await guild.roles.fetch(CONFIG.get().MEMBER_ROLE_ID)
                .catch(err => console.fatal(`Failed to fetch member role : ${err}`))

        DISCORD_CLIENT._everyone_role = guild.roles.everyone
        if (!DISCORD_CLIENT._everyone_role)
            console.fatal('everyone role is not valid')

        // Ensure everyone permissions are less than members permissions
        if ((DISCORD_CLIENT._everyone_role.permissions.bitfield & DISCORD_CLIENT._member_role.permissions.bitfield) === DISCORD_CLIENT._member_role.permissions.bitfield)
            console.fatal(`Everyone role should have less permissions than member roles : \n
everyone = ${DISCORD_CLIENT._everyone_role.permissions.bitfield.toString(2)}\n${DISCORD_CLIENT._everyone_role.permissions.bitfield.toString(2).split("").reverse().join("")} (reversed)\n
member = ${DISCORD_CLIENT._member_role.permissions.bitfield.toString(2)}\n${DISCORD_CLIENT._member_role.permissions.bitfield.toString(2).split("").reverse().join("")} (reversed)`)

        // Ensure members permissions are less than admins permissions
        if ((DISCORD_CLIENT._member_role.permissions.bitfield & DISCORD_CLIENT._member_role.permissions.bitfield) === DISCORD_CLIENT._admin_role.permissions.bitfield)
            console.fatal(`Member role should have less permissions than admin roles : \n
member = ${DISCORD_CLIENT._member_role.permissions.bitfield.toString(2)}\n${DISCORD_CLIENT._member_role.permissions.bitfield.toString(2).split("").reverse().join("")} (reversed)
admin = ${DISCORD_CLIENT._admin_role.permissions.bitfield.toString(2)}\n${DISCORD_CLIENT._admin_role.permissions.bitfield.toString(2).split("").reverse().join("")} (reversed)`)
    }

    everyone_role_id() {
        return this._everyone_role.id
    }

    member_role_id() {
        return this._everyone_role.id
    }

    admin_role_id() {
        return this._everyone_role.id
    }

    get_role_permissions(role_id) {
        const guild = this._client.guilds.cache.get(CONFIG.get().SERVER_ID)
        const role = guild.roles.cache.get(role_id)
        if (!role)
            console.fatal(`failed to find role ${role_id}`)
        return BigInt(role.permissions.bitfield)
    }

    _on_thread_create(thread) {
        if (DISCORD_CLIENT.on_thread_create)
            DISCORD_CLIENT.on_thread_create(thread)
    }

    _on_message(msg) {
        if (DISCORD_CLIENT.on_message && !msg.author.bot)
            DISCORD_CLIENT.on_message(msg)
        else if (msg.author.bot && msg.type === 6) { // MessageType.ChannelPinnedMessage (6)
            msg.delete() // Remove pin messages from bot
        }
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
        const guild = await this._client.guilds.fetch(CONFIG.get().SERVER_ID)
        const members = await guild.members.fetch()
        return members.filter(member => member.user.bot === false).size
    }

    set_activity(message) {
        this._client.user.setPresence({activities: [{name: message, type: ActivityType.Watching}]})
    }

    async set_slash_commands(commands) {
        const command_data = []
        const command_set = new Set()
        for (const command of commands) {
            command_set.add(command.name)
            const discord_command = new SlashCommandBuilder()
                .setName(command.name)
                .setDescription(command.description)

            // Commands that can be executed by everyone are also available in DM
            if (!command.has_permission(this.get_role_permissions(this.everyone_role_id())))
                discord_command.setDMPermission(false)

            if ((command.required_permissions() & this.get_role_permissions(this.everyone_role_id())) !== 0n)
                discord_command.setDefaultMemberPermissions(command.required_permissions())

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
                    case 'channel':
                        discord_command.addChannelOption(opt =>
                            opt.setName(option.name)
                                .setDescription(option.description)
                                .setRequired(option.required)
                        )
                        break
                    case 'message':
                        discord_command.addStringOption(opt =>
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
        const rest = new REST().setToken(CONFIG.get().APP_TOKEN); // don't remove this semicolon

        // Delete old commands
        console.info(`Started refreshing ${command_data.length} application (/) commands.`)

        const current_commands = await rest.get(Routes.applicationCommands(CONFIG.get().APP_ID))

        for (const command of current_commands) {
            if (!command_set.has(command.name)) {
                console.warning(`Removed outdated command ${command.name}`)
                await rest.delete(`${Routes.applicationCommands(CONFIG.get().APP_ID)}/${command.id}`)
            } else
                command_set.delete(command.name)
        }

        if (command_data.length !== current_commands.length) {
            const data2 = await rest.put(Routes.applicationCommands(CONFIG.get().APP_ID), {body: command_data})
            console.info(`Successfully added ${data2.length} application (/) commands.`)
        } else
            console.warning("Nothing to update")
    }

    async check_updates() {
        if (!this._updater)
            console.fatal('updater is null')
        return await this._updater.compareVersions().catch(err => {
            console.fatal(`Failed to get version ${err}`)
        })
    }
}

async function init(client, updater) {
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