const {Message} = require("./message")
const {User} = require("./user");
const CONFIG = require('../config')
const DI = require('../utils/discord_interface')

class CommandInfo {
    constructor(name, description) {
        this.name = name
        this.description = description
        this.options = []
        this._required_roles = []
    }

    /**
     * Add option to this command
     * @param name {string} the display name of the option
     * @param description {string}
     * @param choices {string[]}
     * @param required {boolean}
     * @param default_value {string|null}
     * @returns {CommandInfo}
     */
    add_text_option(name, description, choices = [], required = true, default_value = null) {
        this._add_option_internal('text', name, description, choices, required, default_value)
        return this
    }

    /**
     * @param name {string}
     * @param description {string}
     * @param required {boolean}
     * @param default_value {boolean | null}
     * @returns {CommandInfo}
     */
    add_bool_option(name, description, required = true, default_value = null) {
        this._add_option_internal('bool', name, description, [], required, default_value)
        return this
    }

    /**
     * Add option requiring an user as input
     * @param name {string}
     * @param description {string}
     * @param required {boolean}
     * @param default_value {boolean | null}
     * @returns {CommandInfo}
     */
    add_user_option(name, description, required = true, default_value = null) {
        this._add_option_internal('user', name, description, [], required, default_value)
        return this
    }

    /**
     * Make this option available for admin only
     * @returns {CommandInfo}
     */
    set_admin_only() {
        this._required_roles = [CONFIG.get().MEMBER_ROLE_ID, CONFIG.get().ADMIN_ROLE_ID]
        return this
    }

    /**
     * Make this option available for members only (or admins)
     * @returns {CommandInfo}
     */
    set_member_only() {
        this._required_roles = [CONFIG.get().MEMBER_ROLE_ID]
        return this
    }

    /**
     * Does input permission flags are enough for this command ?
     * @param permissions {BigInt|number}
     * @returns {boolean}
     */
    has_permission(permissions) {
        const required_permissions = this.required_permissions()
        return (BigInt(permissions) & BigInt(required_permissions)) === required_permissions
    }

    /**
     * Get required permission flags
     * @returns {bigint}
     */
    required_permissions() {
        let required_permissions = 0n
        for (const role_id of this._required_roles)
            required_permissions |= DI.get().get_role_permissions(role_id)
        return required_permissions
    }

    _add_option_internal(type, name, description, choices, required, default_value) {
        const option = {
            type: type,
            name: name.toLowerCase(),
            description: description,
            choices: choices,
            required: required,
            default_value: default_value
        }
        this.options.push(option)
        return option
    }
}

class Interaction {
    /**
     * @param source_command {CommandInfo}
     * @param discord_interaction {Discord.BaseInteraction}
     */
    constructor(source_command, discord_interaction) {
        this._source_command = source_command
        this._interaction = discord_interaction
        this._options = {}
        this._author = new User(discord_interaction.user)
        this._channel = discord_interaction.channelId
        this._context_permissions = discord_interaction.memberPermissions ? discord_interaction.memberPermissions.bitfield : 0n

        if (source_command)
            for (const option of source_command.options)
                this._options[option.name] = option.default_value

        if (discord_interaction.options)
            for (const option of discord_interaction.options._hoistedOptions)
                this._options[option.name] = option.value
    }

    /**
     * Check if this is the right command name
     * @param name {string} tested name
     * @returns {boolean}
     */
    match(name) {
        return name === this._source_command.name
    }

    /**
     * Reply to this command
     * @param message {Message}
     * @param interaction_callback {function|null}
     * @returns {Promise<number|null>} message id
     */
    async reply(message, interaction_callback = null) {
        try {
            const res = await this._interaction.reply(message._output_to_discord())
                .catch(err => console.fatal(`failed to reply to command : ${err}`))
            if (interaction_callback)
                DI.get().module_manager.event_manager().watch_interaction(res.id, interaction_callback)
            return res.id
        } catch (err) {
            console.error(`failed to reply to command : ${err}`, message)
        }
    }

    /**
     * Send acknowledge, and notify the client it's interaction is in process
     * @returns {Promise<Message>}
     */
    async wait_reply() {
        await this._interaction.deferReply({ephemeral: true})
    }

    /**
     * Acknowledge the interaction with a 'vu' message, then delete the reply
     * @returns {Promise<void>}
     */
    async skip() {
        try {
            await this._interaction.reply(new Message().set_text('Vu !').set_client_only()._output_to_discord())
                .catch(err => console.error(`Failed to respond : ${err}`))
        } catch (err) {
            console.error(`Failed to respond : ${err}`)
        }
        try {
            await this._interaction.deleteReply()
                .catch(err => console.error(`Failed to respond : ${err}`))
        } catch (err) {
            console.error(`Failed to delete reply : ${err}`)
        }
    }

    /**
     * Read option value
     * @param option {string} option name
     * @returns {*|null}
     */
    read(option) {
        return this._options[option]
    }

    /**
     * Delete reply
     * @returns {Promise<void>}
     */
    async delete_reply() {
        await this._interaction.deleteReply()
    }

    /**
     * Edit reply
     * @param message {Message}
     * @returns {Promise<Discord.Message>}
     */
    async edit_reply(message) {
        await this._interaction.editReply(message._output_to_discord())
    }

    /**
     * Get initial command infos
     * @returns {CommandInfo}
     */
    source_command() {
        return this._source_command
    }

    /**
     * Get the channel id this interaction have been sent
     * @returns {number}
     */
    channel() {
        return this._channel
    }

    /**
     * Get permission flags
     * @returns {bigint}
     */
    context_permissions() {
        return this._context_permissions
    }

    /**
     * Get the author of this interaction
     * @returns {Promise<User>}
     */
    async author() {
        return this._author
    }
}

module.exports = {Interaction, CommandInfo}