const Discord = require("discord.js");
const CONFIG = require("./config.js").get()

class Embed {
    constructor() {
        this.fields = []
    }

    set_title(name) {
        this.title = name
        return this
    }

    set_description(description) {
        this.description = description
        return this
    }

    set_thumbnail(thumbnail) {
        this.thumbnail = thumbnail
        return this
    }

    add_field(name, value, inline = false) {
        this.fields.push({name: name, value: value, inline: inline})
        return this
    }
}

class Button {
    constructor(id, label) {
        this.label = label
        this.id = id
    }

    set_primary() {
        this.type = 'Primary'
        return this
    }

    set_secondary() {
        this.type = 'Secondary'
        return this
    }

    set_success() {
        this.type = 'Success'
        return this
    }

    set_danger() {
        this.type = 'Danger'
        return this
    }

    set_link() {
        this.type = 'Link'
        return this
    }

    to_discord_item() {
        return new Discord.ButtonBuilder()
            .setCustomId(this.id)
            .setLabel(this.label)
            .setStyle(this.type)
    }
}

class Message {
    constructor() {
        this.embeds = []
        this.interactions = []
    }

    set_author(id, name) {
        this.author = {
            id: id,
            name: name
        }
        return this
    }

    set_text(text) {
        this.text = text
        return this
    }

    set_source_id(source_id) {
        this.source_id = source_id
        return this
    }

    set_channel(id) {
        this.channel = id
        return this
    }

    set_dm(is_dm) {
        this.dm = is_dm
        return this
    }

    add_embed(embed) {
        this.embeds.push(embed)
        return this
    }

    add_interaction_row(row) {
        this.interactions.push(row)
        return this
    }

    from_discord(discord_message) {
        this.set_author(discord_message.author.id, `${discord_message.author.username}#${discord_message.author.discriminator}`)
        this.set_text(discord_message.content)
        this.set_source_id(discord_message.id)
        this.set_channel(discord_message.channelId)
        this.set_dm(!discord_message.guildId)
        return this
    }

    is_empty() {
        return this.text === null && this.embeds.length === 0
    }

    set_client_only() {
        this.client_only = true
        return this
    }

    output_to_discord() {
        if (this.is_empty()) {
            console.log('cannot send empty message')
            return
        }

        let embeds = []

        for (const embed of this.embeds) {
            const item = new Discord.EmbedBuilder()
                .setDescription(embed.description)
                .setTitle(embed.title)
                .setThumbnail(embed.thumbnail)

            for (const field of embed.fields) {
                item.addFields(field)
            }

            embeds.push(item)
        }

        let components = []
        for (const row of this.interactions) {
            const discord_row = new Discord.ActionRowBuilder()
            for (const item of row)
                discord_row.addComponents(item.to_discord_item())
            components.push(discord_row)
        }

        return {
            content: this.text,
            embeds: embeds,
            ephemeral: this.client_only,
            components: components,
        }
    }

    async _get_discord_message(client) {
        if (!this.channel) {
            console.log('Missing channel')
            return
        }

        if (!this.source_id) {
            console.log('Missing source id')
        }

        const channel = client.channels.cache.get(this.channel)
        if (!channel) {
            console.log('Unknown channel')
            return;
        }

        return await channel.messages.fetch(this.source_id)
    }

    delete(client) {
        this._get_discord_message(client).then(di_message => {
            di_message.delete()
                .then(res => console.log(res))
                .catch(err => console.log(`Failed to delete message : ${err}`))
        })
    }

    update(client, new_message) {
        this._get_discord_message(client)
            .then(message => {
                message.edit(new_message.output_to_discord())
                    .catch(err => console.log(`Failed to update message : ${err}`))
            })
            .catch(err => console.log(`Failed to update message : ${err}`))
    }
}

class CommandInfo {
    constructor(name, description) {
        this.name = name
        this.description = description
        this.options = []
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
        this._admin_only = true
        this._member_only = false
        return this
    }

    set_member_only() {
        this._admin_only = false
        this._member_only = true
        return this
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

class Command {
    constructor(info, discord_interaction) {
        this.info = info
        this._interaction = discord_interaction
        this.is_server = !!discord_interaction.guildId
        this._options = {}
        this.owner = discord_interaction.user.id
        this.channel = discord_interaction.channelId

        for (const option of info.options)
            this._options[option.name] = option.default_value
        for (const option of discord_interaction.options._hoistedOptions)
            this._options[option.name] = option.value
    }

    match(name) {
        return name === this.info.name
    }

    async reply(message) {
        const res = await this._interaction.reply(message.output_to_discord())
        return res.id
    }

    skip() {
        this._interaction.reply(new Message().set_text('Vu !').set_client_only().output_to_discord())
        this._interaction.deleteReply()
    }

    option_value(option) {
        return this._options[option]
    }

    delete_reply() {
        this._interaction.deleteReply()
    }

    edit_reply(message) {
        this._interaction.editReply(message.output_to_discord())
    }
}

function refresh_slash_commands(client, commands) {
    const command_data = [];
    for (const command of commands) {
        const discord_command = new Discord.SlashCommandBuilder()
            .setName(command.name)
            .setDescription(command.description)

        if (command._member_only || command._admin_only)
            discord_command.setDMPermission(false)

        if (command._member_only)
            discord_command.setDefaultMemberPermissions(CONFIG.ADMIN_PERMISSION_FLAG)

        if (command._member_only)
            discord_command.setDefaultMemberPermissions(CONFIG.USER_PERMISSION_FLAG)

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
        command_data.push(discord_command.toJSON());
    }

    // Construct and prepare an instance of the REST module
    const rest = new Discord.REST().setToken(CONFIG.TOKEN);

    // deploy old
    (async () => {
        try {
            console.log(`Started refreshing ${command_data.length} application (/) commands.`);

            // The put method is used to fully refresh all old in the guild with the current set
            const data2 = await rest.put(
                Discord.Routes.applicationCommands(CONFIG.APP_ID),
                {body: command_data},
            );
            console.log(`Successfully reloaded ${data2.length} application (/) commands.`);
        } catch (error) {
            // And of course, make sure you catch and log any errors!
            console.error(error);
        }
    })();
}

function patch_client(client) {
    client.say = async (message) => {
        if (!message.channel) {
            console.log('please provide a channel')
            return
        }
        const res = await client.channels.cache.get(message.channel).send(message.output_to_discord())

        return new Message().from_discord(res)
    }

    client.get_user_name = (client_id, full = false) => {
        if (!client_id) return console.log('invalid client id')
        const user = client.users.cache.get(client_id)
        return user.username + (full ? "#" + user.discriminator : '')
    }

    client.get_user_icon = (client_id) => {
        if (!client_id) return console.log('invalid client id')
        const user = client.users.cache.get(client_id)
        return `https://cdn.discordapp.com/avatars/${user.id}/${user.avatar}.png`
    }

    client.get_message = async (channel_id, message_id) => {
        if (!message_id) return console.log('invalid message id')

        const channel = client.channels.cache.get(channel_id)
        const msg = await channel.messages.fetch(message_id)
        return new Message().from_discord(msg)
    }
}

module.exports = {Message, refresh_slash_commands, Command, CommandInfo, patch_client, Embed, Button}