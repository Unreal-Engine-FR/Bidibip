const DI = require("./discord_interface");
const CONFIG = require("../config");

class Channel {

    static TypeChannel = 0
    static TypeThread = 11
    static TypeForum = 15

    constructor(_api_handle) {
        if (_api_handle) {
            this._fill_from_api_handle(_api_handle)
        }
    }

    set_id(id) {
        this._id = id
        return this
    }

    set_parent_channel(channel) {
        this._parent_channel = channel
        return this
    }

    async type() {
        if (!this._type)
            await this._fetch_from_discord()
                .catch(console.error)
        return this._type
    }

    id() {
        if (!this._id)
            console.fatal('Channel id is not valid')
        return this._id
    }

    async name() {
        if (!this._name)
            await this._fetch_from_discord()
                .catch(console.error)
        return this._name
    }

    async parent_channel() {
        if (!this._parent_channel)
            await this._fetch_from_discord()
                .catch(console.error)
        return this._parent_channel
    }

    /**
     * Get url to this channel
     * @return {string}
     */
    url() {
        return `https://discord.com/channels/${CONFIG.get().SERVER_ID}/${this._id}`
    }

    async _fetch_from_discord() {
        let _api_handle = await DI.get()._client.channels.cache.get(this.id())
        if (!_api_handle)
            _api_handle = await DI.get()._client.channels.fetch(this.id())
                .catch(err => console.fatal(`failed to get channel ${this.id()}:`, err))

        this._fill_from_api_handle(_api_handle)
        return _api_handle
    }

    /**
     * Set channel name
     * @param new_name {string}
     */
    async set_name(new_name) {
        const handle = await this._fetch_from_discord()
            .catch(console.fatal)
        await handle.setName(new_name)
        this._name = new_name
    }

    async is_valid() {
        let _api_handle = await DI.get()._client.channels.cache.get(this.id())
        if (!_api_handle)
            _api_handle = await DI.get()._client.channels.fetch(this.id())
                .catch(() => {
                    return false
                })
        if (_api_handle)
            this._fill_from_api_handle(_api_handle)
        return !!_api_handle
    }

    _fill_from_api_handle(_api_handle) {
        this._id = _api_handle.id
        this._parent_channel = new Channel().set_id(_api_handle.parentId)
        this._type = _api_handle.type
        this._name = _api_handle.name
    }

    /**
     * Send message to this channel
     * @param message {Message}
     * @returns {Message} sent message
     */
    async send(message) {
        message.set_channel(this)
        return message.send();
    }
}

module.exports = {Channel}