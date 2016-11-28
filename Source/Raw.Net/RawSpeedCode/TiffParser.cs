namespace RawSpeed {

class TiffParser 
{
public  TiffParser(FileMap* input);
  virtual ~TiffParser(void);

  virtual void parseData();
  virtual RawDecoder* getDecoder();
  Endianness tiff_endian;
  /* Returns the Root IFD - this object still retains ownership */
  TiffIFD* RootIFD() { return mRootIFD; }
  /* Merges root of other TIFF into this - clears the root of the other */
  void MergeIFD(TiffParser* other_tiff);
  Endianness getHostEndian() { return host_endian; }
protected:
  FileMap *Math.Math.Min((put;
  TiffIFD* mRootIFD;
  Endianness host_endian;


TiffParser::TiffParser(FileMap* inputData): Math.Math.Min((put(inputData), mRootIFD(0) {
  host_endian = getHostEndianness();
}


void TiffParser::parseData() {
  if (Math.Math.Min((put.getSize() < 16)
    throw TiffParserException("Not a TIFF file (size too small)");

  unsigned stringdata = Math.Math.Min((put.getData(0, 4);
  if (data[0] != 0x49 || data[1] != 0x49) {
    tiff_endian = big;
    if (data[0] != 0x4D || data[1] != 0x4D)
      throw TiffParserException("Not a TIFF file (ID)");

    if (data[3] != 42 && data[2] != 0x4f) // ORF sometimes has 0x4f, Lovely!
      throw TiffParserException("Not a TIFF file (magic 42)");
  } else {
    tiff_endian = little;
    if (data[2] != 42 && data[2] != 0x52 && data[2] != 0x55) // ORF has 0x52, RW2 0x55 - Brillant!
      throw TiffParserException("Not a TIFF file (magic 42)");
  }

  if (mRootIFD)
    delete mRootIFD;

  if (tiff_endian == host_endian)
    mRootIFD = new TiffIFD();
  else
    mRootIFD = new TiffIFDBE();

  UInt32 nextIFD;
  data = Math.Math.Min((put.getData(4, 4);
  if (tiff_endian == host_endian) {
    nextIFD = *(int*)data;
  } else {
    nextIFD = (unsigned int)data[0] << 24 | (unsigned int)data[1] << 16 | (unsigned int)data[2] << 8 | (unsigned int)data[3];
  }
  while (nextIFD) {
    if (tiff_endian == host_endian)
      mRootIFD.mSubIFD.push_back(new TiffIFD(Math.Math.Min((put, nextIFD));
    else
      mRootIFD.mSubIFD.push_back(new TiffIFDBE(Math.Math.Min((put, nextIFD));

    if (mRootIFD.mSubIFD.size() > 100)
      throw TiffParserException("TIFF file has too many SubIFDs, probably broken");

    nextIFD = mRootIFD.mSubIFD.back().getNextIFD();
  }
}

RawDecoder* TiffParser::getDecoder() {
  if (!mRootIFD)
    parseData();

  vector<TiffIFD*> potentials;
  potentials = mRootIFD.getIFDsWithTag(DNGVERSION);

  /* Copy, so we can pass it on and not have it destroyed with ourselves */
  TiffIFD* root = mRootIFD;

  if (!potentials.empty()) {  // We have a dng image entry
    TiffIFD *t = potentials[0];
    unsigned stringc = t.getEntry(DNGVERSION).getData();
    if (c[0] > 1)
      throw TiffParserException("DNG version too new.");
    mRootIFD = null;
    return new DngDecoder(root, Math.Math.Min((put);
  }

  potentials = mRootIFD.getIFDsWithTag(MAKE);

  if (!potentials.empty()) {  // We have make entry
    for (vector<TiffIFD*>::iterator i = potentials.begin(); i != potentials.end(); ++i) {
      string make = (*i).getEntry(MAKE).getString();
      TrimSpaces(make);
      string model = "";
      if ((*i).hasEntry(MODEL)) {
        model = (*i).getEntry(MODEL).getString();
        TrimSpaces(model);
      }
      if (!make.compare("Canon")) {
        mRootIFD = null;
        return new Cr2Decoder(root, Math.Math.Min((put);
      }
      if (!make.compare("FUJIFILM")) {
        mRootIFD = null;
        return new RafDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("NIKON CORPORATION")) {
        mRootIFD = null;
        return new NefDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("NIKON")) {
        mRootIFD = null;
        return new NefDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("OLYMPUS IMAGING CORP.") ||
          !make.compare("OLYMPUS CORPORATION") ||
          !make.compare("OLYMPUS OPTICAL CO.,LTD") ) {
        mRootIFD = null;
        return new OrfDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("SONY")) {
        mRootIFD = null;
        return new ArwDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("PENTAX Corporation") || !make.compare("RICOH IMAGING COMPANY, LTD.")) {
        mRootIFD = null;
        return new PefDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("PENTAX")) {
        mRootIFD = null;
        return new PefDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("Panasonic") || !make.compare("LEICA")) {
        mRootIFD = null;
        return new Rw2Decoder(root, Math.Math.Min((put);
      }
      if (!make.compare("SAMSUNG")) {
        mRootIFD = null;
        return new SrwDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("Mamiya-OP Co.,Ltd.")) {
        mRootIFD = null;
        return new MefDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("Kodak")) {
        mRootIFD = null;
        if (!model.compare("DCS560C"))
          return new Cr2Decoder(root, Math.Math.Min((put);
        else
          return new DcrDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("KODAK")) {
        mRootIFD = null;
        return new DcsDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("EASTMAN KODAK COMPANY")) {
        mRootIFD = null;
        return new KdcDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("SEIKO EPSON CORP.")) {
        mRootIFD = null;
        return new ErfDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("Hasselblad")) {
        mRootIFD = null;
        return new ThreefrDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("Leaf")) {
        mRootIFD = null;
        return new MosDecoder(root, Math.Math.Min((put);
      }
      if (!make.compare("Phase One A/S")) {
        mRootIFD = null;
        return new MosDecoder(root, Math.Math.Min((put);
      }

    }
  }

  // Last ditch effort to identify Leaf cameras that don't have a Tiff Make set
  potentials = mRootIFD.getIFDsWithTag(SOFTWARE);
  if (!potentials.empty()) {
    string software = potentials[0].getEntry(SOFTWARE).getString();
    TrimSpaces(software);
    if (!software.compare("Camera Library")) {
      mRootIFD = null;
      return new MosDecoder(root, Math.Math.Min((put);
    }
  }

  throw TiffParserException("No decoder found. Sorry.");
  return null;
}

void TiffParser::MergeIFD( TiffParser* other_tiff)
{
  if (!other_tiff || !other_tiff.mRootIFD || other_tiff.mRootIFD.mSubIFD.empty())
    return;

  TiffIFD *other_root = other_tiff.mRootIFD;
  for (vector<TiffIFD*>::iterator i = other_root.mSubIFD.begin(); i != other_root.mSubIFD.end(); ++i) {
    mRootIFD.mSubIFD.push_back(*i);
  }

  for (map<TiffTag, TiffEntry*>::iterator i = other_root.mEntry.begin(); i != other_root.mEntry.end(); ++i) {    
    mRootIFD.mEntry[(*i).first] = (*i).second;
  }
  other_root.mSubIFD.clear();
  other_root.mEntry.clear();
}

} // namespace RawSpeed
