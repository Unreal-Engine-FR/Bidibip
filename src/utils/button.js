const {ButtonBuilder} = require("discord.js");

class Button {
    static Primary = 1
    static Secondary = 2
    static Success = 3
    static Danger = 4
    static Link = 5

    constructor(_api_handle) {
        this._type = Button.Primary
        if (_api_handle) {
            this._type = _api_handle.data.style
            this._label = _api_handle.data.label
            this._id = _api_handle.data.custom_id
        }
    }

    /**
     * Set button unique id
     * @param id {string}
     * @returns {Button}
     */
    set_id(id) {
        this._id = id
        return this
    }

    /**
     * Set button label
     * @param label {string}
     * @returns {Button}
     */
    set_label(label) {
        this._label = label
        return this
    }

    /**
     * Set button display type
     * @param type {number}
     * @returns {Button}
     */
    set_type(type) {
        this._type = type
        return this
    }

    _to_discord_item() {
        if (!this._id)
            console.fatal(`undefined id :`, this)
        return new ButtonBuilder()
            .setCustomId(this._id)
            .setLabel(this._label)
            .setStyle(this._type)
    }
}

module.exports = {Button}