const Discord = require("discord.js")
const {User} = require("./user")
const {Embed} = require("./embed");
const {InteractionRow} = require("./interaction_row");
const DI = require("./discord_interface");
const {Channel} = require("./channel");

class Message {
    constructor(api_handle) {
        this._embeds = []
        this._interactions = []
        if (api_handle)
            this._from_discord_message(api_handle)
    }

    /**
     * Set the person who sent the message
     * @param user {User} author of this message
     * @returns {Message}
     */
    set_author(user) {
        this._author = user
        return this
    }

    /**
     * Set message text
     * @param text {string}
     * @returns {Message}
     */
    set_text(text) {
        this._text = text && text.length === 0 ? null : text
        return this
    }

    /**
     * Set initial message discord id (used to get extra infos about messages, or to get the discord identifier of the initial message)
     * @param id {number}
     * @returns {Message}
     */
    set_id(id) {
        this._id = id
        return this
    }

    /**
     * Set channel ID (this is the channel the message will be sent to)
     * @param channel {Channel}
     * @returns {Message}
     */
    set_channel(channel) {
        this._channel = channel
        return this
    }

    /**
     * Add an embed.js to this message
     * @param embed {Embed}
     * @returns {Message}
     */
    add_embed(embed) {
        this._embeds.push(embed)
        return this
    }

    /**
     * Add a row of user.js button under this message
     * @param row {InteractionRow}
     * @returns {Message}
     */
    add_interaction_row(row) {
        this._interactions.push(row)
        return this
    }

    /**
     * Does this message contains any text or embed.js
     * @returns {boolean}
     */
    is_empty() {
        let text = this._text
        return (!text || text.length === 0) && this._embeds.length === 0
    }

    /**
     * Set message ephemeral (and visible only by the client. Used with interactions)
     * @returns {Message}
     */
    set_client_only(value = true) {
        this._client_only = value
        return this
    }

    /**
     * @returns {Promise<User>}
     */
    async author() {
        if (!this._author)
            await this._fill_internal()
        return this._author
    }

    /**
     * Get text
     * @returns {Promise<string>}
     */
    async text() {
        if (!this._text && this.is_empty())
            await this._fill_internal()
        return this._text
    }

    /**
     * Get id
     * @returns {number}
     */
    id() {
        return this._id
    }

    /**
     * Get channel id
     * @returns {Channel}
     */
    channel() {
        return this._channel
    }

    /**
     * Have this message been sent in DM
     * @returns {boolean}
     */
    is_dm() {
        return this._is_dm
    }

    async _fill_internal() {
        if (!this.channel)
            console.fatal('Missing channel')

        if (!this._id)
            console.fatal('Missing source id')

        let channel = DI.get()._client.channels.cache.get(this._channel.id())
        if (!channel) {
            channel = DI.get()._client.channels.fetch(this._channel.id())
                .catch(err => {
                    console.fatal(`Failed to get channel : ${err}`)
                })
        }

        const di_message = await channel.messages.fetch(this._id)
            .catch(err => {
                console.fatal(`Failed to fetch message ${this._id}/${this._channel}: ${err}`)
            })

        this._from_discord_message(di_message)
        return di_message
    }

    clear_interactions() {
        this._interactions = []
        return this
    }

    _from_discord_message(_api_handle) {
        this._author = new User(_api_handle.author)
        this._text = _api_handle.content
        this._id = _api_handle.id
        this._channel = new Channel(_api_handle.channelId)
        this._is_dm = !_api_handle.guildId
        this._embeds = []
        this._interactions = []
        if (_api_handle.embeds)
            for (const embed of _api_handle.embeds)
                this._embeds.push(new Embed(embed))

        if (_api_handle.components)
            for (const component of _api_handle.components)
                this._interactions.push(new InteractionRow(component))

        return this
    }

    _output_to_discord() {
        if (this.is_empty()) {
            console.fatal(`cannot send empty message : `, this)
            return
        }

        let embeds = []

        for (const embed of this._embeds) {
            if (!embed.title || !embed.description)
                console.fatal(`embed is empty : ${embed}`)

            const item = new Discord.EmbedBuilder()
                .setTitle(embed.title)
                .setDescription(embed.description)
                .setThumbnail(embed.thumbnail)

            for (const field of embed.fields)
                item.addFields(field)

            embeds.push(item)
        }

        let components = []
        for (const row of this._interactions)
            components.push(row._to_discord_row())

        return {
            content: this._text,
            embeds: embeds,
            ephemeral: this._client_only,
            components: components,
        }
    }

    /**
     * Send this message
     * @returns {Message} sent message
     */
    async send() {
        if (!this._channel) {
            console.fatal('please provide a channel')
            return
        }
        if (this.is_empty())
            console.fatal(`cannot send empty message : `, this)

        let channel = await DI.get()._client.channels.cache.get(this._channel.id())
        if (!channel)
            channel = await DI.get()._client.channels.fetch(this._channel.id())
                .catch(err => console.fatal(`failed to get channel ${this._channel.id()}:`, err))

        const res = await channel.send(this._output_to_discord())

        return new Message(res)
    }

    /**
     * Delete this message
     */
    delete() {
        this._fill_internal().then(di_message => {
            di_message.delete()
                .catch(err => console.fatal(`Failed to delete message : ${err}`))
        })
    }

    /**
     * Replace this message with a new one
     * @param new_message {Message}
     */
    update(new_message) {
        this._fill_internal()
            .then(message => {
                message.edit(new_message._output_to_discord())
                    .catch(err => console.fatal(`Failed to update message : ${err}`))
            })
            .catch(err => console.fatal(`Failed to update message : ${err}`))
    }
}

module.exports = {Message}