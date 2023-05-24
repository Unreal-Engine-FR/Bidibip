const {Channel} = require("./channel");
const {User} = require("./user");

class Thread {
    constructor(_api_handle) {
        if (_api_handle) {
            this._owner = new User().set_id(_api_handle.ownerId)
            this._channel = new Channel(_api_handle)
        }
    }

    set_channel(channel) {
        this._channel = channel
        return this
    }

    set_owner(user) {
        this._owner = user
        return this
    }

    channel() {
        return this._channel
    }

    owner() {
        return this._owner
    }
}

module.exports = {Thread}