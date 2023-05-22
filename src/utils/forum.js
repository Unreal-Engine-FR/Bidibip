const {Channel} = require('./channel')

class Forum {
    constructor(_api_handle) {
        if (_api_handle) {
            this._from_api_handle(_api_handle)
        }
    }

    set_channel(channel) {
        this._channel = channel
    }

    channel() {
        return this._channel
    }

    set_id(id) {
        this._channel = new Channel().set_id(id)
        return this
    }

    _from_api_handle(_api_handle) {
        this._channel = new Channel(_api_handle)
    }
}

module.exports = {Forum}