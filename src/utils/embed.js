
class Embed {
    constructor(_api_handle) {
        this.fields = []

        if (_api_handle)
            this._from_discord_api(_api_handle)
    }

    /**
     * Set embed title text
     * @param title {string}
     * @returns {Embed}
     */
    set_title(title) {
        this.title = title
        return this
    }


    /**
     * Set embed title author
     * @param user {User}
     * @returns {Embed}
     */
    set_author(user) {
        this.title = user._name
        return this
    }

    /**
     * Set embed description text
     * @param description {string}
     * @returns {Embed}
     */
    set_description(description) {
        this.description = description
        return this
    }

    /**
     * Set embed thumbnail image
     * @param thumbnail {string} url
     * @returns {Embed}
     */
    set_thumbnail(thumbnail) {
        this.thumbnail = thumbnail
        return this
    }

    /**
     * Add field to embed (a field contains a name and a description)
     * @param name {string} field name
     * @param value {string} field text
     * @param inline {boolean} should be displayed as inline bloc
     * @returns {Embed}
     */
    add_field(name, value, inline = false) {
        this.fields.push({name: name, value: value, inline: inline})
        return this
    }

    _from_discord_api(_api_handle) {
        this.title = _api_handle.title
        this.description = _api_handle.description
        this.thumbnail = _api_handle.thumbnail ? _api_handle.thumbnail.url : null
        if (_api_handle.fields)
            for (const field of _api_handle.fields) {
                this.add_field(field.name, field.value, field.inline)
            }
    }
}

module.exports = {Embed}