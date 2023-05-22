const {ButtonBuilder} = require("discord.js");

class Button {
    constructor(_api_handle) {
        if (_api_handle) {
            this._label = _api_handle.label
            this._id = _api_handle.custom_id
            this._type = _api_handle.style
        }
    }

    static Primary = 1
    static Secondary = 2
    static Success = 3
    static Danger = 4
    static Link = 5

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
        return new ButtonBuilder()
            .setCustomId(this._id)
            .setLabel(this._label)
            .setStyle(this._type)
    }
}

module.exports = {Button}