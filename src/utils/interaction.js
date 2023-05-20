const {Message} = require("./message")
const {User} = require("./user");
const CONFIG = require('../config')

class CommandInfo {
    constructor(name, description) {
        this.name = name
        this.description = description
        this.options = []
        this._min_permissions = 0n
    }

    add_text_option(name, description, choices = [], required = true, default_value = null) {
        this._add_option_internal('text', name, description, choices, required, default_value)
        return this
    }

    add_bool_option(name, description, required = true, default_value = null) {
        this._add_option_internal('bool', name, description, [], required, default_value)
        return this
    }

    add_user_option(name, description, required = true, default_value = null) {
        this._add_option_internal('user', name, description, [], required, default_value)
        return this
    }

    set_admin_only() {
        this._min_permissions = BigInt(CONFIG.get().ADMIN_PERMISSION_FLAG)
        return this
    }

    set_member_only() {
        this._min_permissions = BigInt(CONFIG.get().MEMBER_PERMISSION_FLAG) | BigInt(CONFIG.get().ADMIN_PERMISSION_FLAG)
        return this
    }

    has_permission(permissions) {
        return this._min_permissions === 0n || (this._min_permissions & BigInt(permissions)) !== 0n
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
    constructor(source_command, discord_interaction) {
        this._source_command = source_command
        this._interaction = discord_interaction
        this._options = {}
        this._author = new User(discord_interaction.user)
        this._channel = discord_interaction.channelId
        this._permissions = discord_interaction.memberPermissions.bitfield

        if (source_command)
            for (const option of source_command.options)
                this._options[option.name] = option.default_value

        if (discord_interaction.options)
            for (const option of discord_interaction.options._hoistedOptions)
                this._options[option.name] = option.value
    }

    match(name) {
        return name === this._source_command.name
    }

    async reply(message) {
        try {
            const res = await this._interaction.reply(message._output_to_discord())
            return res.id
        } catch (err) {
            console.error(`failed to reply to command : ${err}`)
            return null
        }
    }

    async wait_reply() {
        await this._interaction.deferReply({ephemeral: true})
    }

    async skip() {
        try {
            await this._interaction.reply(new Message().set_text('Vu !').set_client_only()._output_to_discord())
        } catch (err) {
            console.error(`Failed to respond : ${err}`)
        }
        try {
            await this._interaction.deleteReply()
        } catch (err) {
            console.error(`Failed to delete reply : ${err}`)
        }
    }

    read(option) {
        return this._options[option]
    }

    async delete_reply() {
        await this._interaction.deleteReply()
    }

    async edit_reply(message) {
        await this._interaction.editReply(message._output_to_discord())
    }

    source_command() {
        return this._source_command
    }

    channel() {
        return this._channel
    }

    set_permission(permission_flags) {
        this._permissions = permission_flags
    }

    permissions() {
        return this._permissions
    }

    async author() {
        return this._author
    }
}

module.exports = {Interaction, CommandInfo}