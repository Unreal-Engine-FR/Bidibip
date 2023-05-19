class Configuration {
    constructor() {

        this.TOKEN = null
        this.APP_ID = null
        this.SERVER_ID = null
        this.LOG_CHANNEL_ID = null
        this.OFFER_CHANNEL_ID = null
        this.ROLE_ONLY = null
        this.ROLE_MEMBER = null

        this.COLOR_MESSAGE_DELETED = '#998d00'
        this.COLOR_QUOTE = '0x0099ff'
        this.COLOR_NEW_QUOTE = '#993300'
        this.COLOR_OFFER = '#00ccff'

        this.COLOR_PAID = '#998d00'
        this.COLOR_UNPAID = '0x0099ff'
        this.COLOR_FREELANCE = '993300'

        this.ADVERT_UNPAID_ID = null
        this.ADVERT_PAID_ID = null
        this.ADVERT_FREELANCE_ID = null
        this.SHARED = null

        this.USER_PERMISSION_FLAG=16384 // Send link
        this.ADMIN_PERMISSION_FLAG=4 // Ban member

        this.UPDATE_CACHE='../Bidibip_Cache/update/'
        this.LOG_DIR='../Bidibip_Cache/logs/'
        this.LOG_KEEP_DAYS=14
        this.SERVICE_ROLE='<@285426910404673536>'

        require('dotenv').config();
        for (const [key, value] of Object.entries(process.env)) {
            this[key] = value
        }
    }
}

let CONFIG = null

function get() {
    if (CONFIG === null)
        CONFIG = new Configuration()
    return CONFIG
}

module.exports = {get}