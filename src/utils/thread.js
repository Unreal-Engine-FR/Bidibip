const {Channel} = require("./channel");
const {User} = require("./user");
const {Message} = require("./message");

class Thread extends Channel {
    constructor(_api_handle) {
        super(_api_handle)
        if (_api_handle) {
            this._fill_from_api_handle(_api_handle)
        }
    }

    async owner() {
        if (!this._owner)
            await this._fetch_from_discord()
        return this._owner
    }

    /**
     * @return {Promise<boolean>}
     */
    async archived() {
        if (!typeof this._archived === 'boolean')
            await this._fetch_from_discord();
        return this._archived;
    }

    _fill_from_api_handle(_api_handle) {
        super._fill_from_api_handle(_api_handle)
        this._owner = new User().set_id(_api_handle.ownerId)
        this._archived = _api_handle.archived
    }

    first_message() {
        return new Message().set_id(this._id).set_channel(this)
    }
}

module.exports = {Thread}