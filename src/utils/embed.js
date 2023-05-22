
class Embed {
    constructor() {
        this.fields = []
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
}

module.exports = {Embed}