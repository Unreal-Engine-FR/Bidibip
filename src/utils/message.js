const Discord = require("discord.js")
const {User} = require("./user")
const {Embed} = require("./embed");
const {InteractionRow} = require("./interaction_row");
const DI = require("./discord_interface");
const {Channel} = require("./channel");
const {Attachment} = require("./attachment");
const CONFIG = require("../config");
const assert = require("assert");

class Message {
    constructor(api_handle) {
        this._embeds = []
        this._interactions = []
        this._attachments = []

        if (api_handle) {
            if (api_handle.partial) {
                this._id = api_handle.id
                this._channel = new Channel(api_handle.channel)
            } else
                this._from_discord_message(api_handle)
        }
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
     * @param id {number|string}
     * @returns {Message}
     */
    set_id(id) {
        if (typeof(id) !== 'number' && typeof(id) !== 'string')
            console.fatal("Id should be a number : ", id);
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
     * Add attachment to this message
     * @param attachment {Attachment}
     * @returns {Message}
     */
    add_attachment(attachment) {
        this._attachments.push(attachment)
        return this
    }

    /**
     * Get attachments
     * @return {Attachment[]}
     */
    attachments() {
        return this._attachments
    }

    /**
     * @returns {Message}
     */
    clear_attachments() {
        this._attachments = []
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
                .catch(err => {
                    throw new Error(`failed to retrieve internal data : ${err}`)
                })
        return this._author
    }

    /**
     * Get text
     * @returns {Promise<string>}
     */
    async text() {
        if (!this._text && this.is_empty())
            await this._fill_internal()
                .catch(err => console.fatal(`failed to retrieve internal data : ${err}`))
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

    /**
     * Get url to this message
     * @return {string}
     */
    url() {
        return `https://discord.com/channels/${CONFIG.get().SERVER_ID}/${this._channel.id()}/${this._id}`
    }

    /**
     * Get embeds
     * @return {Embed[]}
     */
    embeds() {
        return this._embeds
    }

    first_embed() {
        return this.embeds()[0]
    }

    /**
     * Retrieve button by id
     * @param id {string}
     * @returns {Button|null}
     */
    async get_button_by_id(id) {
        if (!this._author)
            await this._fill_internal()
        for (const row of this._interactions)
            for (const object of row._items)
                if (object._id === id)
                    return object
        return null
    }

    async _fill_internal() {
        this._from_discord_message(await this._internal_get_handle())
    }

    async _internal_get_handle() {
        if (!this._channel)
            console.fatal('Missing channel')

        if (!this._id)
            console.fatal('Missing source id')

        let channel = DI.get()._client.channels.cache.get(this._channel.id())
        if (!channel) {
            channel = await DI.get()._client.channels.fetch(this._channel.id())
                .catch(err => {
                    throw new Error(`Failed to get channel : ${err}`)
                })
        }

        return await channel.messages.fetch(this._id)
            .catch(err => {
                throw new Error(`Failed to fetch message ${this._id} : ${err}`)
            })
    }

    async is_valid() {
        try {
            await this._fill_internal(await this._internal_get_handle())
            return true
        } catch (_) {
            return false
        }
    }

    async exists() {
        const message = await this._internal_get_handle().catch(_ => {
            return false
        })
        return Boolean(message)
    }

    clear_interactions() {
        this._interactions = []
        return this
    }

    async react(emoji) {
        const handle = await this._internal_get_handle()
        await handle.react(emoji);
    }

    async reactions() {
        const reactions = []
        const reaction_manager = await this._internal_get_handle()
        for (const [k, _] of reaction_manager.reactions.cache)
            reactions.push(k)
        return reactions
    }

    _from_discord_message(_api_handle) {
        this._author = new User(_api_handle.author)
        this._text = _api_handle.content
        this._id = _api_handle.id
        this._channel = new Channel().set_id(_api_handle.channelId)
        this._is_dm = !_api_handle.guildId
        this._embeds = []
        this._interactions = []
        if (_api_handle.embeds)
            for (const embed of _api_handle.embeds) {
                const embed_object = new Embed(embed)
                if (embed_object.is_valid())
                    this._embeds.push(embed_object)
            }

        this._interactions = []
        if (_api_handle.components)
            for (const component of _api_handle.components)
                this._interactions.push(new InteractionRow(component))

        this._attachments = []
        if (_api_handle.attachments)
            for (const [_, v] of _api_handle.attachments.entries())
                this._attachments.push(new Attachment(v))

        return this
    }

    async _output_to_discord() {
        try {
            if (this.is_empty()) {
                console.fatal(`cannot send empty message : `, this)
                return
            }

            let embeds = []

            for (const embed of this._embeds) {
                embeds.push(await embed._to_discord_api())
            }

            let components = []
            for (const row of this._interactions)
                components.push(row._to_discord_row())

            const files = []
            for (const attachment of this._attachments)
                files.push({attachment: attachment.file(), name: attachment.name()})

            return {
                content: this._text,
                embeds: embeds,
                ephemeral: this._client_only,
                components: components,
                files: files
            }
        } catch (err) {
            console.fatal(`Message is not valid : ${err}\nMessage :`, this, '\nError : ', err)
        }
    }

    /**
     * Send this message
     * @returns {Promise<Message>} sent message
     */
    async send() {
        if (!this._channel) {
            console.fatal('please provide a channel')
        }
        if (this.is_empty())
            console.fatal(`cannot send empty message : `, this)

        let channel = await DI.get()._client.channels.cache.get(this._channel.id())
        if (!channel)
            channel = await DI.get()._client.channels.fetch(this._channel.id())
                .catch(err => console.fatal(`failed to get channel ${this._channel.id()}:`, err))

        const res = await channel.send(await this._output_to_discord().catch(err => console.fatal('Failed to build message :', err)))
            .catch(err => console.fatal(`failed to send message : ${err} :  `, this))
        return new Message(res)
    }

    /**
     * Pin this message
     * @returns {Promise<void>}
     */
    async pin() {
        await this._internal_get_handle()
            .then(di_message => {
                di_message.pin()
                    .catch(err => console.fatal(`Failed to pin message : ${err}`))
            })
    }

    /**
     * Delete this message
     */
    async delete() {
        const di_message = await this._internal_get_handle()
            .catch(err => console.fatal(`Failed to delete message : ${err}`))
        await di_message.delete()
            .catch(err => console.fatal(`Failed to delete message : ${err}`))
    }

    /**
     * Replace this message with a new one
     * @param new_message {Message}
     */
    async update(new_message) {
        const message = await this._internal_get_handle()
            .catch(err => {
                throw new Error(`Failed to retrieve message : ${err}`)
            })

        await message.edit(await new_message._output_to_discord())
            .catch(err => {
                throw new Error(`Failed to update message : ${err}`)
            })
    }
}

module.exports = {Message}